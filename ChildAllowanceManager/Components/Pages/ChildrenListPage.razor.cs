using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.DependencyInjection;
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

    [Parameter]
    public string? TenantSuffix { get; set; }

    private string? _tenantId = null;
    private bool CanManageCurrentTenant;
    private ChildWithBalance[]? Children = null;
    private readonly SemaphoreSlim _dataGate = new(1, 1);
    private readonly SemaphoreSlim _parametersGate = new(1, 1);
    private bool _balanceHistoryNeedsSync = true;
    private ChildWithBalanceHistory[] _balanceHistory = [];

    private readonly TimeSeriesChartOptions _balanceChartOptions = new()
    {
        ChartPalette = ChartColors,
        YAxisLines = false,
        YAxisRequireZeroPoint = true,
        MaxNumYAxisTicks = 6,
        YAxisFormat = "C0",
        XAxisLines = false,
        TimeLabelFormat = "MMM d",
        TimeLabelSpacing = TimeSpan.FromDays(1),
        TooltipTimeLabelFormat = "d MMM yyyy",
        TooltipTitleFormat = "{{SERIES_NAME}}",
        TooltipSubtitleFormat = "Balance: {{Y_VALUE}}",
        ShowDataMarkers = true,
        LineStrokeWidth = 2,
    };

    private List<ChartSeries<decimal>> _balanceChartSeries = [];
    
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
            if (!string.IsNullOrWhiteSpace(TenantSuffix))
            {
                TenantConfiguration? tenant;
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

                if (tenant is null)
                {
                    Navigation.NavigateTo("/error/404");
                    return;
                }

                var previousTenantId = _tenantId;
                _tenantId = tenant.Id;
                if (!await HasTenantAccessAsync(tenant.Id))
                {
                    Navigation.NavigateTo("/");
                    return;
                }
                CanManageCurrentTenant = AuthenticationState is null ||
                    TenantAuthorization.CanManage((await AuthenticationState).User, tenant.Id);
                if (previousTenantId != tenant.Id)
                {
                    _contextUpdated = false;
                    _balanceChartSeries.Clear();
                    _balanceHistory = [];
                }
                await ReloadChildren();
            }

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
            await LocalStorage.SetAsync("current_tenant", _tenantId);
            await LocalStorage.SetAsync("current_tenant_suffix", TenantSuffix!);
            CurrentContextService.SetCurrentTenant(_tenantId);
            Logger.LogInformation("Current tenant updated to {TenantId}", _tenantId);
            _contextUpdated = true;
        }

        if (_balanceHistoryNeedsSync)
        {
            _balanceHistoryNeedsSync = false;
            await SyncChildBalanceHistorySeries();
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
                return;

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
                _tenantId, null, null, CancellationToken)).ToArray());
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
        var parameters = new DialogParameters<AddFundsDialog>();
        parameters.Add(x => x.Child, child);
        await DialogService.ShowAsync<AddFundsDialog>(null, parameters);
    }
    
    private async Task ShowWithdrawFundsForChild(ChildWithBalance child)
    {
        var parameters = new DialogParameters<WithdrawFundsDialog>();
        parameters.Add(x => x.Child, child);
        await DialogService.ShowAsync<WithdrawFundsDialog>(null, parameters);
    }
    
    private async Task ApplyHold(ChildWithBalance child)
    {
        var parameters = new DialogParameters<AddHoldDialog>();
        parameters.Add(x => x.Child, child);
        await DialogService.ShowAsync<AddHoldDialog>(null, parameters);
    }
    
    private async Task RemoveHoldDay(ChildWithBalance child)
    {
        var outcome = await RunAsync(
            async () => await ChildService.RemoveHoldDayAsync(
                child.Id, child.TenantId, requestId: null, CancellationToken),
            successMessage: $"One held day removed for {child.Name}.");
        if (outcome.Succeeded)
            await ReloadChildren();
    }

    public override void Dispose()
    {
        TenantNotificationService.ChildStateChanged -= ChildStateChangedNotification;
        base.Dispose();
    }

    private async Task<bool> HasTenantAccessAsync(string tenantId)
    {
        if (AuthenticationState is null)
            return true;
        return TenantAuthorization.CanView((await AuthenticationState).User, tenantId);
    }
}
