using System.Collections.ObjectModel;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Plotly.Blazor;
using Plotly.Blazor.ConfigLib;
using Plotly.Blazor.LayoutLib;
using Plotly.Blazor.LayoutLib.LegendLib;
using Plotly.Blazor.LayoutLib.YAxisLib;
using Plotly.Blazor.Traces;
using Plotly.Blazor.Traces.ScatterLib;
using Font = Plotly.Blazor.LayoutLib.Font;
using HoverModeEnum = Plotly.Blazor.LayoutLib.HoverModeEnum;
using LegendOrientationEnum = Plotly.Blazor.LayoutLib.LegendLib.OrientationEnum;
using Margin = Plotly.Blazor.LayoutLib.Margin;
using Line = Plotly.Blazor.Traces.ScatterLib.Line;
using Marker = Plotly.Blazor.Traces.ScatterLib.Marker;
using Title = Plotly.Blazor.LayoutLib.YAxisLib.Title;

namespace ChildAllowanceManager.Components.Pages;

public partial class ChildrenListPage : CancellableComponentBase, IDisposable
{
    private static readonly string[] ChartColors = ["#8B5CF6", "#F59E0B", "#14B8A6", "#EC4899"];

    [Inject] 
    private ITenantService TenantService { get; set; } = default!;
    
    [Inject]
    public IChildService ChildService { get; set; } = default!;
    
    [Inject]
    private ITenantNotificationService TenantNotificationService { get; set; } = default!;
    
    [Inject]
    public ITransactionService TransactionService { get; set; } = default!;
    
    [Inject]
    public NavigationManager Navigation { get; set; } = default!;
    
    [Inject]
    public ProtectedLocalStorage LocalStorage { get; set; } = default!;
    
    [Inject]
    public IHttpContextAccessor HttpContextAccessor { get; set; } = default!;
    
    [Inject]
    public ICurrentContextService CurrentContextService { get; set; } = default!;
    
    [Inject]
    public ILogger<ChildrenListPage> Logger { get; set; } = default!;
    
    [Inject]
    public IDialogService DialogService { get; set; } = default!;
    
    [Inject]
    public ISnackbar Snackbar { get; set; } = default!;

    [Inject]
    private IServiceScopeFactory ServiceScopeFactory { get; set; } = default!;

    [Parameter]
    public string? TenantSuffix { get; set; }

    [CascadingParameter]
    public ThemeConfiguration ThemeConfiguration { get; set; } = default!;
    
    private string? _tenantId = null;
    private ChildWithBalance[]? Children = null;
    private readonly SemaphoreSlim _dataGate = new(1, 1);
    private readonly SemaphoreSlim _parametersGate = new(1, 1);
    private bool _balanceHistoryNeedsSync = true;
    private bool _plotlyThemeNeedsSync;
    private bool _plotlyThemeIsDarkMode;
    private string _plotlySurfaceColor = "#FFFFFF";

    #region Plotly
    private Config _plotlyConfig = new()
    {
        DisplayLogo = false,
        AutoSizable = true,
        FrameMargins = 0,
        Editable = false,
        DisplayModeBar = DisplayModeBarEnum.False,
        Locale = "en-GB",
        Responsive = true
    };

    private Plotly.Blazor.Layout _plotlyLayout = new()
    {
        ShowLegend = true,
        HoverMode = HoverModeEnum.XUnified,
        YAxis = new List<YAxis>(){ new Plotly.Blazor.LayoutLib.YAxis()
            {
                TickPrefix = "£",
                ShowTickPrefix = ShowTickPrefixEnum.All,
                ShowTickLabels = true,
                TickFormat = ",.0f",
                GridColor = "rgba(148, 163, 184, .18)",
                GridWidth = 1,
                ZeroLine = true,
                ZeroLineColor = "rgba(148, 163, 184, .38)",
                ZeroLineWidth = 1,
                ShowLine = false,
            }
        },
        XAxis = new List<XAxis>(){ new Plotly.Blazor.LayoutLib.XAxis()
            {
                ShowGrid = false,
                ShowLine = false,
                TickFormat = "%b %-d",
                TickAngle = 0,
            }
        },
        AutoSize = true,
        Margin = new Margin() { T = 24, R = 24, B = 52, L = 58},
        Legend = new List<Legend>()
        {
            new Legend()
            {
                X = 0,
                Y = 1,
                XAnchor = XAnchorEnum.Left,
                YAnchor = YAnchorEnum.Top,
                Orientation = LegendOrientationEnum.H,
            }
        },
    };
    
    private PlotlyChart _plotlyChart = null!; // referenced in razor page
    
    IList<ITrace> _plotlyData = new List<ITrace>();
    #endregion Plotly
    
    protected override async Task OnInitializedAsync()
    {
        
        ApplyPlotlyTheme();
        
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
        this.InvokeAsync(async () => await ReloadChildren());
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_plotlyThemeIsDarkMode != ThemeConfiguration.IsDarkMode)
        {
            ApplyPlotlyTheme();
            _plotlyThemeIsDarkMode = ThemeConfiguration.IsDarkMode;
            _plotlyThemeNeedsSync = true;
        }

        await _parametersGate.WaitAsync(CancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(TenantSuffix))
            {
                TenantConfiguration? tenant;
                await _dataGate.WaitAsync(CancellationToken);
                try
                {
                    await using var scope = ServiceScopeFactory.CreateAsyncScope();
                    var isolatedTenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
                    tenant = await isolatedTenantService.GetTenantBySuffix(TenantSuffix, CancellationToken);
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

                _tenantId = tenant.Id;
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
            await LocalStorage.SetAsync("current_tenant_suffix", TenantSuffix);
            CurrentContextService.SetCurrentTenant(_tenantId);
            Logger.LogInformation("Current tenant updated to {TenantId}", _tenantId);
            _contextUpdated = true;
        }

        if (_balanceHistoryNeedsSync)
        {
            _balanceHistoryNeedsSync = false;
            await SyncChildBalanceHistorySeries();
        }

        if (_plotlyThemeNeedsSync && _plotlyData.Count > 0)
        {
            _plotlyThemeNeedsSync = false;
            await _plotlyChart.React(CancellationToken);
        }
    }

    private void ApplyPlotlyTheme()
    {
        Palette palette = ThemeConfiguration.IsDarkMode
            ? ThemeConfiguration.Theme.PaletteDark
            : ThemeConfiguration.Theme.PaletteLight;
        string surfaceColor = palette.Surface.ToString();
        string textColor = palette.TextPrimary.ToString();
        string mutedColor = palette.TextSecondary.ToString();
        _plotlyLayout.PaperBgColor = surfaceColor;
        _plotlyLayout.PlotBgColor = surfaceColor;
        _plotlyLayout.Font = new Font { Color = textColor };
        _plotlySurfaceColor = surfaceColor;

        if (_plotlyLayout.YAxis?.FirstOrDefault() is YAxis yAxis)
        {
            yAxis.TickColor = mutedColor;
            yAxis.GridColor = ThemeConfiguration.IsDarkMode
                ? "rgba(255, 255, 255, .12)"
                : "rgba(23, 32, 51, .10)";
            yAxis.ZeroLineColor = ThemeConfiguration.IsDarkMode
                ? "rgba(255, 255, 255, .32)"
                : "rgba(23, 32, 51, .26)";
        }

        if (_plotlyLayout.XAxis?.FirstOrDefault() is XAxis xAxis)
        {
            xAxis.TickColor = mutedColor;
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
            Children = (await isolatedChildService.GetChildrenWithBalance(_tenantId, CancellationToken)).ToArray();
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
            var balanceHistory = await isolatedChildService.GetChildrenWithBalanceHistory(
                _tenantId, null, null, CancellationToken);
            bool changesFound = false;
            foreach (var (child, index) in balanceHistory.Select((child, index) => (child, index)))
            {
                string chartColor = ChartColors[index % ChartColors.Length];

                var existingTrace = _plotlyData.Cast<Scatter>().FirstOrDefault((t) => t.Name == child.ChildName);
                if (existingTrace is null)
                {
                    changesFound = true;
                    _plotlyData.Add(new Plotly.Blazor.Traces.Scatter()
                    {
                        Name = child.ChildName,
                        X = child.BalanceHistory.Select(x => (object)x.Timestamp).ToList(),
                        Y = child.BalanceHistory.Select(x => (object)x.Balance).ToArray(),
                        Mode = ModeFlag.Lines | ModeFlag.Markers,
                        Line = new Line
                        {
                            Color = chartColor,
                            Width = 3,
                        },
                        Marker = new Marker
                        {
                            Color = chartColor,
                            Size = 8,
                            Line = new Plotly.Blazor.Traces.ScatterLib.MarkerLib.Line
                            {
                                Color = _plotlySurfaceColor,
                                Width = 2,
                            }
                        },
                        Fill = index == 0 ? FillEnum.ToZeroY : FillEnum.None,
                        FillColor = index == 0 ? "rgba(139, 92, 246, .12)" : "rgba(245, 158, 11, 0)",
                        HoverTemplate = $"<b>{child.ChildName}</b><br>%{{x|%b %-d, %Y}}<br>Balance: £%{{y:,.2f}}<extra></extra>",
                        XCalendar = XCalendarEnum.Gregorian,
                    });
                }
                else if (existingTrace.X.Count != child.BalanceHistory.Length)
                {
                    changesFound = true;
                    existingTrace.X = child.BalanceHistory.Select(x => (object)x.Timestamp).ToList();
                    existingTrace.Y = child.BalanceHistory.Select(x => (object)x.Balance).ToArray();
                }
            }

            if (changesFound)
                await _plotlyChart.React(CancellationToken);
        }
        finally
        {
            _dataGate.Release();
        }
    }
    
    
    private async Task ShowTransactionsForChild(ChildWithBalance child)
    {
        var parameters = new DialogParameters<ChildTransactionsDialogue>();
        parameters.Add(x => x.Child, child);
        await DialogService.ShowAsync<ChildTransactionsDialogue>(null, parameters);
    }
    
    private async Task ShowAddFundsForChild(ChildWithBalance child)
    {
        var parameters = new DialogParameters<AddFundsDialogue>();
        parameters.Add(x => x.Child, child);
        await DialogService.ShowAsync<AddFundsDialogue>(null, parameters);
    }
    
    private async Task ShowWithdrawFundsForChild(ChildWithBalance child)
    {
        var parameters = new DialogParameters<WithdrawFundsDialogue>();
        parameters.Add(x => x.Child, child);
        await DialogService.ShowAsync<WithdrawFundsDialogue>(null, parameters);
    }
    
    private async Task ApplyHold(ChildWithBalance child)
    {
        var parameters = new DialogParameters<AddHoldDialogue>();
        parameters.Add(x => x.Child, child);
        await DialogService.ShowAsync<AddHoldDialogue>(null, parameters);
    }
    
    private async Task RemoveHoldDay(ChildWithBalance child)
    {
        var childToUpdate = await ChildService.GetChild(child.Id, child.TenantId, CancellationToken);
        if (childToUpdate is null)
        {
            await DialogService.ShowMessageBoxAsync(
                title: "Error",
                message: "Child not found",
                yesText: "OK");
            return;
        }
        childToUpdate.HoldDaysRemaining--;
        await ChildService.UpdateChild(childToUpdate, CancellationToken);
        await ReloadChildren();
    }

    public override void Dispose()
    {
        TenantNotificationService.ChildStateChanged -= ChildStateChangedNotification;
        base.Dispose();
    }
}
