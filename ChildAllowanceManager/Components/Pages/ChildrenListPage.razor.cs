using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor;

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
    
    private string? _tenantId = null;
    private ChildWithBalance[]? Children = null;
    private Dictionary<string, ChartSeries> ChildBalanceHistorySeries = new();
    private HubConnection? hubConnection;

    
    protected override async Task OnInitializedAsync()
    {
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
    }

    private async Task ReloadChildren()
    {
        if (_tenantId is null)
        {
            return;
        }
        Children = (await DataService.GetChildrenWithBalance(_tenantId, CancellationToken)).ToArray();
        foreach (var child in Children)
        {
            ChildBalanceHistorySeries[child.Id] = await GetChildBalanceHistorySeries(child) ?? new();
        }
        StateHasChanged();
    }

    async Task<ChartSeries> GetChildBalanceHistorySeries(ChildWithBalance child)
    {
        var balanceHistory = await TransactionService.GetBalanceHistoryForChild(child.Id, child.TenantId, null, null, CancellationToken);
        return new ChartSeries
        {
            Name = child.Name,
            Data = balanceHistory.Select(x => (double)x.Balance).ToArray()
        };
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