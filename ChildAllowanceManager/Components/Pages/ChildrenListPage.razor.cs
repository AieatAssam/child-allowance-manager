using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Charts;

namespace ChildAllowanceManager.Components.Pages;

public partial class ChildrenListPage : CancellableComponentBase, IDisposable
{
    // Values come from docs/brand/brand-guidelines.md.
    private static readonly string[] ChartColors = ["#675184", "#32735F", "#B95E4D", "#E9A36A"];

    [Inject]
    public IChildService ChildService { get; set; } = default!;
    
    [Inject]
    private ITenantNotificationService TenantNotificationService { get; set; } = default!;
    
    [Inject]
    public NavigationManager Navigation { get; set; } = default!;
    
    [Inject]
    public ProtectedLocalStorage LocalStorage { get; set; } = default!;
    
    [Inject]
    public ICurrentContextService CurrentContextService { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationState { get; set; }
    
    [Inject]
    public ILogger<ChildrenListPage> Logger { get; set; } = default!;
    
    [Inject]
    public IDialogService DialogService { get; set; } = default!;
    
    [Inject]
    public ISnackbar Snackbar { get; set; } = default!;

    [Inject]
    private IServiceScopeFactory ServiceScopeFactory { get; set; } = default!;

    [Inject]
    private ITenantAuthorizationService TenantAuthorization { get; set; } = default!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter]
    public string? TenantSuffix { get; set; }

    /// Present only on the /share/{shareToken} route. Its presence IS share mode.
    /// Never logged, never stored, never rendered. See S-D11.
    [Parameter]
    public string? ShareToken { get; set; }

    [Inject]
    private IShareLinkService ShareLinkService { get; set; } = default!;

    private bool _shareMode;
    private string? _shareLinkId;

    private string? _tenantId = null;
    private bool CanManageCurrentTenant;
    private ChildWithBalance[]? Children = null;
    private readonly SemaphoreSlim _dataGate = new(1, 1);
    private readonly SemaphoreSlim _parametersGate = new(1, 1);
    private bool _balanceHistoryNeedsSync = true;
    private ChildWithBalanceHistory[] _balanceHistory = [];
    private readonly Dictionary<string, string> _balanceMotionDirections = [];
    private readonly Dictionary<string, int> _balanceMotionVersions = [];

    private bool HasBalanceHistory => _balanceHistory.Any(child => child.BalanceHistory.Length > 0);

    private readonly TimeSeriesChartOptions _balanceChartOptions = new()
    {
        ChartPalette = ChartColors,
        YAxisLines = false,
        YAxisRequireZeroPoint = true,
        MaxNumYAxisTicks = 6,
        YAxisFormat = "C0",
        XAxisLines = false,
        TimeLabelFormat = "MMM d",
        // One label per fortnight. A daily label over a 90-day window renders the axis as an
        // unreadable smear of overlapping dates.
        TimeLabelSpacing = TimeSpan.FromDays(14),
        TooltipTimeLabelFormat = "d MMM yyyy",
        TooltipTitleFormat = "{{SERIES_NAME}}",
        TooltipSubtitleFormat = "Balance: {{Y_VALUE}}",
        ShowDataMarkers = false,
        LineStrokeWidth = 1,
    };

    private List<ChartSeries<decimal>> _balanceChartSeries = [];
    private Task? _sharePollTask;
    private CancellationTokenSource? _sharePollCancellation;
    private string? _activeShareToken;
    private static readonly TimeSpan SharePollInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan BalanceHistoryWindow = TimeSpan.FromDays(90);
    
    protected override async Task OnInitializedAsync()
    {
        TenantNotificationService.ChildStateChanged += ChildStateChangedNotification;

        await ReloadChildren();
    }

    private void ChildStateChangedNotification(object? sender, IGlobalNotificationService.ChildStateChangedEventArgs e)
    {
        Logger.LogDebug("Child {Child} has been updated", e.ChildId);
        if (!string.IsNullOrEmpty(e.NotificationMessage))
        {
            var child = Children?.FirstOrDefault(c => c.Id == e.ChildId);
            if (child is not null)
            {
                Snackbar.Add($"{child.Name}\r\n{e.NotificationMessage}", Severity.Info);
            }
        }
        _ = InvokeAsync(async () => await ReloadChildren());
    }

    protected override async Task OnParametersSetAsync()
    {
        await _parametersGate.WaitAsync(CancellationToken);
        try
        {
            var shareMode = !string.IsNullOrWhiteSpace(ShareToken);
            if (_shareMode != shareMode ||
                !string.Equals(_activeShareToken, ShareToken, StringComparison.Ordinal))
            {
                StopSharePoll();
                _shareMode = shareMode;
                _activeShareToken = shareMode ? ShareToken : null;
            }

            TenantConfiguration? tenant = null;
            if (_shareMode)
            {
                var shareToken = ShareToken!;
                ShareLink? link = null;
                await _dataGate.WaitAsync(CancellationToken);
                try
                {
                    var shareOutcome = await RunAsync(async () =>
                    {
                        await using var scope = ServiceScopeFactory.CreateAsyncScope();
                        var isolatedShareLinkService =
                            scope.ServiceProvider.GetRequiredService<IShareLinkService>();
                        link = await isolatedShareLinkService.ResolveAsync(shareToken, CancellationToken);
                    });
                    if (!shareOutcome.Succeeded)
                        return;
                }
                finally
                {
                    _dataGate.Release();
                }

                if (link is null)
                {
                    Navigation.NavigateTo("/share/expired");
                    return;
                }

                _shareLinkId = link.Id;
                tenant = link.Tenant;
                Logger.LogInformation(
                    "Share link {ShareLinkId} opened for tenant {TenantId}", link.Id, tenant!.Id);
                StartSharePoll(shareToken);
            }
            else if (!string.IsNullOrWhiteSpace(TenantSuffix))
            {
                _shareLinkId = null;
                await _dataGate.WaitAsync(CancellationToken);
                try
                {
                    tenant = null;
                    var tenantOutcome = await RunAsync(async () =>
                    {
                        await using var scope = ServiceScopeFactory.CreateAsyncScope();
                        var isolatedTenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
                        tenant = await isolatedTenantService.GetTenantBySuffix(TenantSuffix, CancellationToken);
                    });
                    if (!tenantOutcome.Succeeded)
                        return;
                }
                finally
                {
                    _dataGate.Release();
                }
            }
            else
            {
                await base.OnParametersSetAsync();
                return;
            }

            if (tenant is null)
            {
                Navigation.NavigateTo("/error/404");
                return;
            }

            var previousTenantId = _tenantId;
            _tenantId = tenant.Id;
            if (!_shareMode && !await HasTenantAccessAsync(tenant.Id))
            {
                Children = null;
                _balanceHistory = [];
                _balanceChartSeries.Clear();
                _balanceHistoryNeedsSync = false;
                var signedIn = AuthenticationState is not null &&
                    (await AuthenticationState).User.Identity?.IsAuthenticated == true;
                Navigation.NavigateTo(signedIn ? "/" : "/login");
                return;
            }
            // A signed-in parent of this family keeps their own permissions even when they
            // open the display link - the link decides who may LOOK, never who may act.
            // Anyone else on a share link is read-only, and a share display is signed in
            // as nobody, so it stays read-only too.
            CanManageCurrentTenant = AuthenticationState is null
                ? !_shareMode
                : TenantAuthorization.CanManage((await AuthenticationState).User, tenant.Id);
            if (previousTenantId != tenant.Id)
            {
                _contextUpdated = false;
                _balanceChartSeries.Clear();
                _balanceHistory = [];
            }
            await ReloadChildren();

            await base.OnParametersSetAsync();
        }
        finally
        {
            _parametersGate.Release();
        }
    }

    private bool _contextUpdated = false;
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_tenantId is not null && !_contextUpdated)
        {
            if (!_shareMode)
            {
                await LocalStorage.SetAsync("current_tenant", _tenantId);
                await LocalStorage.SetAsync("current_tenant_suffix", TenantSuffix!);
            }
            CurrentContextService.SetCurrentTenant(_tenantId);
            Logger.LogInformation("Current tenant updated to {TenantId}", _tenantId);
            _contextUpdated = true;
        }

        if (_balanceHistoryNeedsSync)
        {
            _balanceHistoryNeedsSync = false;
            await SyncChildBalanceHistorySeries();
        }

        await JSRuntime.InvokeVoidAsync("AllowanceMotion.animateBalances");
    }

    private async Task ReloadChildren()
    {
        if (_tenantId is null)
        {
            return;
        }

        await _dataGate.WaitAsync(CancellationToken);
        try
        {
            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            var isolatedChildService = scope.ServiceProvider.GetRequiredService<IChildService>();
            ChildWithBalance[]? loaded = null;
            var outcome = await RunAsync(async () =>
                loaded = (await isolatedChildService.GetChildrenWithBalance(_tenantId, CancellationToken)).ToArray());
            if (!outcome.Succeeded)
                return;

            if (Children is not null)
            {
                foreach (var child in loaded!)
                {
                    var previous = Children.FirstOrDefault(existing => existing.Id == child.Id);
                    if (previous is not null && previous.Balance != child.Balance)
                        RecordBalanceMotion(child.Id, child.Balance - previous.Balance);
                }
            }

            Children = loaded!;
            _balanceHistoryNeedsSync = true;
            StateHasChanged();
        }
        finally
        {
            _dataGate.Release();
        }
    }

    async Task SyncChildBalanceHistorySeries()
    {
        if (_tenantId is null)
        {
            return;
        }

        await _dataGate.WaitAsync(CancellationToken);
        try
        {
            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            var isolatedChildService = scope.ServiceProvider.GetRequiredService<IChildService>();
            ChildWithBalanceHistory[]? balanceHistory = null;
            var outcome = await RunAsync(async () => balanceHistory = (await isolatedChildService.GetChildrenWithBalanceHistory(
                _tenantId, DateTimeOffset.UtcNow.Subtract(BalanceHistoryWindow), null, CancellationToken)).ToArray());
            if (!outcome.Succeeded)
                return;

            _balanceHistory = balanceHistory!;
            var series = new List<ChartSeries<decimal>>();
            foreach (var child in _balanceHistory)
            {
                series.Add(new ChartSeries<decimal>
                {
                    Name = child.ChildName,
                    Data = new ChartData<decimal>(child.BalanceHistory.Select(entry =>
                        (entry.Timestamp.UtcDateTime, entry.Balance)).ToArray()),
                    TooltipYValueFormat = "C2",
                });
            }

            _balanceChartOptions.SeriesDisplayOverrides = series.Count == 0
                ? []
                : new Dictionary<IChartSeries, SeriesDisplayOverride>
                {
                    [series[0]] = new SeriesDisplayOverride
                    {
                        LineDisplayType = LineDisplayType.Area,
                        FillOpacity = 0.12,
                    },
                };
            _balanceChartSeries = series;
            // The sync runs from OnAfterRenderAsync, which does not re-render on its own.
            // Without this the chart panel keeps showing its empty state after data arrives.
            StateHasChanged();
        }
        finally
        {
            _dataGate.Release();
        }
    }
    
    
    private async Task ShowTransactionsForChild(ChildWithBalance child)
    {
        var parameters = new DialogParameters<ChildTransactionsDialog>();
        parameters.Add(x => x.Child, child);
        var options = new DialogOptions
        {
            BackdropClick = true,
            CloseOnEscapeKey = true,
            FullScreen = true,
            FullWidth = true,
            BackgroundClass = "transactions-dialog-background",
        };
        await DialogService.ShowAsync<ChildTransactionsDialog>(null, parameters, options);
    }
    
    private async Task ShowAddFundsForChild(ChildWithBalance child)
    {
        if (!CanManageCurrentTenant)
            return;

        var parameters = new DialogParameters<AddFundsDialog>();
        parameters.Add(x => x.Child, child);
        await DialogService.ShowAsync<AddFundsDialog>(null, parameters);
    }
    
    private async Task ShowWithdrawFundsForChild(ChildWithBalance child)
    {
        if (!CanManageCurrentTenant)
            return;

        var parameters = new DialogParameters<WithdrawFundsDialog>();
        parameters.Add(x => x.Child, child);
        await DialogService.ShowAsync<WithdrawFundsDialog>(null, parameters);
    }
    
    private async Task ApplyHold(ChildWithBalance child)
    {
        if (!CanManageCurrentTenant)
            return;

        var parameters = new DialogParameters<AddHoldDialog>();
        parameters.Add(x => x.Child, child);
        await DialogService.ShowAsync<AddHoldDialog>(null, parameters);
    }
    
    private async Task RemoveHoldDay(ChildWithBalance child)
    {
        if (!CanManageCurrentTenant)
            return;

        var outcome = await RunAsync(
            async () => await ChildService.RemoveHoldDayAsync(
                child.Id, child.TenantId, requestId: null, CancellationToken),
            successMessage: $"One held day removed for {child.Name}.");
        if (outcome.Succeeded)
            await ReloadChildren();
    }

    private void RecordBalanceMotion(string childId, decimal difference)
    {
        _balanceMotionDirections[childId] = difference > 0 ? "in" : "out";
        _balanceMotionVersions[childId] = GetBalanceMotionVersion(childId) + 1;
    }

    private string GetBalanceMotionDirection(string childId) =>
        _balanceMotionDirections.GetValueOrDefault(childId, string.Empty);

    private int GetBalanceMotionVersion(string childId) =>
        _balanceMotionVersions.GetValueOrDefault(childId);

    private static string GetHoldDaysLabel(int days) =>
        $"{days} more {(days == 1 ? "day" : "days")}";

    private static int GetChildAccentIndex(string childId)
    {
        var hash = 17;
        foreach (var character in childId)
            hash = (hash * 31 + character) & int.MaxValue;
        return hash % 4;
    }

    private void StartSharePoll(string shareToken)
    {
        if (!_shareMode || _sharePollTask is not null)
            return;
        var cancellation = new CancellationTokenSource();
        _sharePollCancellation = cancellation;
        _sharePollTask = PollShareLinkAsync(shareToken, cancellation.Token);
    }

    private void StopSharePoll()
    {
        _sharePollCancellation?.Cancel();
        _sharePollCancellation?.Dispose();
        _sharePollCancellation = null;
        _sharePollTask = null;
    }

    private async Task PollShareLinkAsync(string shareToken, CancellationToken pollCancellationToken)
    {
        // ponytail: polling, not push. A revocation takes up to 5 minutes to reach an open
        // display. Push it through ITenantNotificationService if instant revocation matters.
        using var timer = new PeriodicTimer(SharePollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(pollCancellationToken))
            {
                await using var scope = ServiceScopeFactory.CreateAsyncScope();
                var isolatedShareLinkService =
                    scope.ServiceProvider.GetRequiredService<IShareLinkService>();
                var link = await isolatedShareLinkService.ResolveAsync(shareToken, pollCancellationToken);
                if (link is null)
                {
                    if (pollCancellationToken.IsCancellationRequested)
                        return;
                    await InvokeAsync(() => Navigation.NavigateTo("/share/expired", forceLoad: true));
                    return;
                }
                if (pollCancellationToken.IsCancellationRequested)
                    return;
                await InvokeAsync(ReloadChildren);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public override void Dispose()
    {
        TenantNotificationService.ChildStateChanged -= ChildStateChangedNotification;
        StopSharePoll();
        base.Dispose();
    }

    private async Task<bool> HasTenantAccessAsync(string tenantId)
    {
        if (AuthenticationState is null)
            return true;

        var user = (await AuthenticationState).User;
        return user.Identity?.IsAuthenticated == true && TenantAuthorization.CanView(user, tenantId);
    }
}
