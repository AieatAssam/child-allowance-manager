using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor;
using Plotly.Blazor;
using Plotly.Blazor.ConfigLib;
using Plotly.Blazor.LayoutLib;
using Plotly.Blazor.LayoutLib.YAxisLib;
using Plotly.Blazor.Traces;
using Plotly.Blazor.Traces.ScatterLib;
using Margin = Plotly.Blazor.LayoutLib.Margin;
using Title = Plotly.Blazor.LayoutLib.YAxisLib.Title;

namespace ChildAllowanceManager.Components.Pages;

public partial class ChildrenListPage : CancellableComponentBase
{
    [Inject]
    public IDataService DataService { get; set; } = default!;
    
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

    [Parameter]
    public string? TenantSuffix { get; set; }

    [CascadingParameter]
    public ThemeConfiguration ThemeConfiguration { get; set; } = default!;
    
    private string? _tenantId = null;
    private ChildWithBalance[]? Children = null;
    private HubConnection? hubConnection;

    #region Plotly
    private Config _plotlyConfig = new()
    {
        DisplayLogo = false,
        AutoSizable = true,
        FrameMargins = 0,
        Editable = false,
        DisplayModeBar = DisplayModeBarEnum.False,
        Locale = "en-GB"
    };

    private Plotly.Blazor.Layout _plotlyLayout = new()
    {
        ShowLegend = true,
        YAxis = new List<YAxis>(){ new Plotly.Blazor.LayoutLib.YAxis()
            {
                TickPrefix = "£",
                ShowTickPrefix = ShowTickPrefixEnum.All,
                ShowTickLabels = true,
            }
        },
        Margin = new Margin() { T = 40, R = 40, B = 40, L = 40},
    };
    
    private PlotlyChart _plotlyChart;
    
    IList<ITrace> _plotlyData = new List<ITrace>();
    #endregion Plotly
    
    protected override async Task OnInitializedAsync()
    {
        
        Palette palette = ThemeConfiguration.IsDarkMode
            ? ThemeConfiguration.Theme.PaletteDark
            : ThemeConfiguration.Theme.PaletteLight;
        _plotlyLayout.PaperBgColor = palette.Surface.ToString();
        _plotlyLayout.PlotBgColor = palette.Surface.ToString();
        _plotlyLayout.Font = new Font
        {
            Color = palette.TextPrimary.ToString()
        };

        await ReloadChildren();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!string.IsNullOrWhiteSpace(TenantSuffix))
        {
            var tenant = await DataService.GetTenantBySuffix(TenantSuffix, CancellationToken);
            if (tenant is null)
            {
                Navigation.NavigateTo("/error/404");
                return;
            }

            _tenantId = tenant.Id;
            
            hubConnection = new HubConnectionBuilder()
                .WithUrl(Navigation.ToAbsoluteUri($"/notifications?tenant={_tenantId}"))
                .WithAutomaticReconnect()
                .Build();

            hubConnection.On("AllowanceUpdated", async () =>
            {
                await InvokeAsync(ReloadChildren);
            });

            await hubConnection.StartAsync();
            
            await ReloadChildren();
        }

        await base.OnParametersSetAsync();
    }

    private bool _contextUpdated = false;
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_tenantId is not null && !_contextUpdated)
        {
            await LocalStorage.SetAsync("current_tenant", _tenantId);
            CurrentContextService.SetCurrentTenant(_tenantId);
            Logger.LogInformation("Current tenant updated to {TenantId}", _tenantId);
        }

        await SyncChildBalanceHistorySeries();
    }

    private async Task ReloadChildren()
    {
        if (_tenantId is null)
        {
            return;
        }
        Children = (await DataService.GetChildrenWithBalance(_tenantId, CancellationToken)).ToArray();
        StateHasChanged();
    }

    async Task SyncChildBalanceHistorySeries()
    {
        if (_tenantId is null)
        {
            return;
        }

        var balanceHistory = await DataService.GetChildrenWithBalanceHistory(_tenantId, null, null, CancellationToken);
        bool changesFound = false;
        foreach (var child in balanceHistory)
        {

            var existingTrace = _plotlyData.Cast<Scatter>().FirstOrDefault((t) => t.Name == child.ChildName);
            if (existingTrace is null)
            {
                changesFound = true;
                _plotlyData.Add(new Plotly.Blazor.Traces.Scatter()
                {
                    Name = child.ChildName,
                    X = child.BalanceHistory.Select(x => (object)x.Timestamp).ToList(),
                    Y = child.BalanceHistory.Select(x => (object)x.Balance).ToArray(),
                    Mode = ModeFlag.Lines,
                    Fill = FillEnum.ToZeroY,
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
        var childToUpdate = await DataService.GetChild(child.Id, child.TenantId, CancellationToken);
        if (childToUpdate is null)
        {
            var error = new MudMessageBox()
            {
                Message = "Child not found",
                YesText = "OK"
            };
            await error.ShowAsync();
            return;
        }
        childToUpdate.HoldDaysRemaining--;
        await DataService.UpdateChild(childToUpdate, CancellationToken);
        await ReloadChildren();
    }
}