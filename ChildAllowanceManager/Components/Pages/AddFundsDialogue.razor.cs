using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace ChildAllowanceManager.Components.Pages;

public partial class AddFundsDialogue : CancellableComponentBase
{
    [CascadingParameter] private MudDialogInstance MudDialog { get; set; } = default!;
    
    [Parameter] public ChildWithBalance Child { get; set; } = default!;
    
    [Inject] private ITransactionService TransactionService { get; set; } = default!;
    
    private decimal _amount;
    private string _description;
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
    
    private void RoundUp()
    {
        var expectedBalance = Child.Balance + Amount;
        var decimalPart = expectedBalance % 1;
        if (decimalPart > 0)
        {
            Amount += 1 - decimalPart;
        }
    }

    
    private async Task AddFunds()
    {
        await _form.Validate();
        if (!_form.IsValid)
            return;
        await TransactionService.AddTransaction(new AllowanceTransaction
        {
            Description = Description,
            TransactionAmount = Amount,
            TenantId = Child.TenantId,
            ChildId = Child.Id,
            TransactionType = TransactionType.Deposit
        }, CancellationToken);
        MudDialog.Close();
    }
}