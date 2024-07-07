using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChildAllowanceManager.Components;

public partial class ChildTransactionsTable : CancellableComponentBase
{
    [Parameter] public ChildWithBalance? Child { get; set; }
    
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
            var data = await TransactionService.GetPagedTransactionsForChild(Child.Id, Child.TenantId, tableState.Page,
                tableState.PageSize, _ignoreDailyTransactions, token);
            return new TableData<AllowanceTransaction>
                {
                    Items = data.Items,
                    TotalItems = data.Total ?? data.TotalPages * tableState.PageSize ?? 0
                };
        }

        return new TableData<AllowanceTransaction>();
    }
}