using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChildAllowanceManager.Components.Pages;

public partial class AddHoldDialogue : CancellableComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;
    
    [Parameter] public ChildWithBalance Child { get; set; } = default!;
    
    [Inject] private ITransactionService TransactionService { get; set; } = default!;
    
    [Inject] private IChildService ChildService { get; set; } = default!;
    
    [Inject] private IDialogService DialogService { get; set; } = default!;

    public int Days { get; set; } = 1;
    
    public string Description { get; set; } = string.Empty;

    private MudForm _form = null!;
    
    private async Task AddHold()
    {
        await _form.Validate();
        if (!_form.IsValid)
            return;
        // update child
        var child = await ChildService.GetChild(Child.Id, Child.TenantId);
        if (child is null)
        {
            await DialogService.ShowMessageBox(title:"Error", message: "Child not found",
                yesText: "OK");
            MudDialog.Cancel();
            return;
        }
        child.HoldDaysRemaining += Days;
        await ChildService.UpdateChild(child, CancellationToken);
        await TransactionService.AddTransaction(new AllowanceTransaction
        {
            Description = Description + $" ({Days} days)",
            TransactionAmount = 0, // no amount
            TenantId = Child.TenantId,
            ChildId = Child.Id,
            TransactionType = TransactionType.Hold
        }, CancellationToken);
        MudDialog.Close();
    }
}