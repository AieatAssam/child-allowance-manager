using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChildAllowanceManager.Components.Pages;

public partial class AddHoldDialogue : ComponentBase
{
    [CascadingParameter] private MudDialogInstance MudDialog { get; set; } = default!;
    
    [Parameter] public ChildWithBalance Child { get; set; } = default!;
    
    [Inject] private ITransactionService TransactionService { get; set; } = default!;
    
    [Inject] private IDataService DataService { get; set; } = default!;

    public int Days { get; set; } = 1;
    
    public string Description { get; set; } = FilterOperator.String.Empty;
    
    private async Task AddHold()
    {
        // update child
        var child = await DataService.GetChild(Child.Id, Child.TenantId);
        child.HoldDaysRemaining += Days;
        await DataService.UpdateChild(child, CancellationToken.None);
        await TransactionService.AddTransaction(new AllowanceTransaction
        {
            Description = Description + $" ({Days} days)",
            TransactionAmount = 0, // no amount
            TenantId = Child.TenantId,
            ChildId = Child.Id,
            TransactionType = TransactionType.Hold
        });
        MudDialog.Close();
    }
}