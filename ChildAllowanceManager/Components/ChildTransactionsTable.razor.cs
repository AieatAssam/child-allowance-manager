using System.ComponentModel.DataAnnotations;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChildAllowanceManager.Components;

public partial class ChildTransactionsTable : CancellableComponentBase
{
    [Parameter, Required] public ChildWithBalance? Child { get; set; }

    [Inject] private IBrowserViewportService BrowserViewportService { get; set; } = default!;

    
    [Inject]
    public ITransactionService TransactionService { get; set; } = default!;

    private bool _ignoreDailyTransactions = false;
    private List<AllowanceTransaction> _transactions = new();
    private MudTable<AllowanceTransaction> _table;
    
    protected override async Task OnParametersSetAsync()
    {
        if (Child is not null)
        {
            StateHasChanged();
        }
    }
    
    private async Task<TableData<AllowanceTransaction>> RetrievePage(TableState tableState, CancellationToken token)
    {
        if (Child is not null)
        {
            var data = await TransactionService.GetPagedTransactionsForChild(
                Child.Id, 
                Child.TenantId, 
                tableState.Page + 1, // ICosmosRepository uses 1-based page numbers and MudBlazor uses 0-based
                tableState.PageSize, _ignoreDailyTransactions, token);
            return new TableData<AllowanceTransaction>
                {
                    Items = data.Items,
                    TotalItems = data.Total ?? data.TotalPages * tableState.PageSize ?? 0
                };
        }

        return new TableData<AllowanceTransaction>();
    }
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await BrowserViewportService.SubscribeAsync(this, fireImmediately: true);
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    public async ValueTask DisposeAsync() => await BrowserViewportService.UnsubscribeAsync(this);
    Task IBrowserViewportObserver.NotifyBrowserViewportChangeAsync(BrowserViewportEventArgs browserViewportEventArgs)
    {
        var currentDensity = _table.Dense;
        IsSmallSize = browserViewportEventArgs.Breakpoint is Breakpoint.Sm or Breakpoint.Xs;
        if (IsSmallSize == currentDensity) return Task.CompletedTask;
#pragma warning disable BL0005
        _table.Dense = IsSmallSize;
#pragma warning restore BL0005
        return InvokeAsync(StateHasChanged);
    }
    Guid IBrowserViewportObserver.Id { get; } = Guid.NewGuid();

    private bool IsSmallSize { get; set; } = false;
}