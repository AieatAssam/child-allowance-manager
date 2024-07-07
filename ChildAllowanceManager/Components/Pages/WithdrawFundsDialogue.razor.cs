using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChildAllowanceManager.Components.Pages;

public partial class WithdrawFundsDialogue : CancellableComponentBase
{
    [CascadingParameter] private MudDialogInstance MudDialog { get; set; } = default!;
    
    [Parameter] public ChildWithBalance Child { get; set; } = default!;
    
    [Inject] private ITransactionService TransactionService { get; set; } = default!;
    
    private decimal _amount;
    private string _description = string.Empty;
    private MudForm _form;
    
    public decimal Amount
    {
        get => _amount;
        set => _amount = value;
    }
    
    public string Description
    {
        get => _description;
        set => _description = value;
    }

    private async Task WithdrawFunds()
    {
        await _form.Validate();
        if (!_form.IsValid)
            return;
        await TransactionService.AddTransaction(new AllowanceTransaction
        {
            Description = Description,
            TransactionAmount = -Amount,
            TenantId = Child.TenantId,
            ChildId = Child.Id,
            TransactionType = TransactionType.Withdrawal
        }, CancellationToken);
        MudDialog.Close();
    }
}