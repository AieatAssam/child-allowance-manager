using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChildAllowanceManager.Components.Pages;

public partial class AddFundsDialog : CancellableComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;
    
    [Parameter] public ChildWithBalance Child { get; set; } = default!;
    
    [Inject] private ITransactionService TransactionService { get; set; } = default!;
    
    private decimal _amount;
    private string _description = string.Empty;
    private MudForm _form = default!;
    private readonly string _requestId = Guid.NewGuid().ToString("N");

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
        await _form.ValidateAsync();
        if (!_form.IsValid)
            return;
        var outcome = await RunAsync(
            async () => await TransactionService.AddTransaction(new AllowanceTransaction
            {
                Description = Description,
                TransactionAmount = Amount,
                TenantId = Child.TenantId,
                ChildId = Child.Id,
                TransactionType = TransactionType.Deposit,
                RequestId = _requestId
            }, CancellationToken),
            successMessage: $"Added {Amount:C2} to {Child.Name}.");

        if (outcome.Succeeded)
            MudDialog.Close();
    }
}
