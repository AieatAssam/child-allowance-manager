using System.Security.Cryptography;
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
    // Series colours, one per child. Values come from docs/brand/brand-guidelines.md and must
    // stay in step with --al-chart-* in tokens.css, which paints the legend and card accents.
    private static readonly string[] ChartColors =
        ["#4C6FE7", "#1FA463", "#E8573F", "#C98209", "#8B5BD6"];

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
    private string? _loadError;

    private string? _tenantId = null;
    private bool CanManageCurrentTenant;
    private ChildWithBalance[]? Children = null;
    private Dictionary<string, int> _accentByChildId = [];
    private readonly SemaphoreSlim _dataGate = new(1, 1);
    private readonly SemaphoreSlim _parametersGate = new(1, 1);
    private bool _balanceHistoryNeedsSync = true;
    private bool _balanceHistoryLoading = true;
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
                _balanceHistoryLoading = true;
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
                try
                {
                    await LocalStorage.SetAsync("current_tenant", _tenantId);
                    await LocalStorage.SetAsync("current_tenant_suffix", TenantSuffix!);
                }
                catch (Exception ex) when (ex is JSException or InvalidOperationException or CryptographicException)
                {
                    // Storage is unreachable in a third-party iframe or with site data blocked.
                    // Letting this escape OnAfterRenderAsync kills the circuit and blanks the page.
                    Logger.LogDebug(ex, "Could not remember the current family; browser storage is unavailable.");
                }
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

        try
        {
            await JSRuntime.InvokeVoidAsync("AllowanceMotion.animateBalances");
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            Logger.LogDebug(ex, "Balance motion is unavailable; leaving balances static.");
        }
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
            {
                _loadError = outcome.ErrorMessage ?? "Unable to load balances.";
                StateHasChanged();
                return;
            }

            _loadError = null;
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
            AssignChildAccents(Children.Select(child => child.Id));
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
            {
                _balanceHistoryLoading = false;
                return;
            }

            _balanceHistory = balanceHistory!;
            _balanceHistoryLoading = false;
            if (_accentByChildId.Count == 0)
                AssignChildAccents(_balanceHistory.Select(child => child.ChildId));

            // The palette is positional, so order it to match the series: every child keeps the
            // colour their card border already shows.
            _balanceChartOptions.ChartPalette = _balanceHistory
                .Select(child => ChartColors[GetChildAccentIndex(child.ChildId)])
                .ToArray();

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

    /// The colour a child owns, everywhere they appear - card border, chart line, legend
    /// swatch. Assigned by position in an id-ordered list rather than by hashing the id, so
    /// two children in the same family can never land on the same colour.
    private int GetChildAccentIndex(string childId) =>
        _accentByChildId.TryGetValue(childId, out var index) ? index : 0;

    private void AssignChildAccents(IEnumerable<string> childIds)
    {
        _accentByChildId = childIds
            .Distinct()
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index % ChartColors.Length);
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
