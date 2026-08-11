using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChildAllowanceManager.Components.Pages;

public partial class WithdrawFundsDialog : CancellableComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;
    
    [Parameter] public ChildWithBalance Child { get; set; } = default!;
    
    [Inject] private ITransactionService TransactionService { get; set; } = default!;
    
    private decimal _amount;
    private string _description = string.Empty;
    private MudForm _form = default!;
    private readonly string _requestId = Guid.NewGuid().ToString("N");
    private bool _overdrawAcknowledged;

    private decimal ResultingBalance => Child.Balance - Amount;
    private bool WillOverdraw => ResultingBalance < 0;
    
    public decimal Amount
    {
        get => _amount;
        set
        {
            _amount = value;
            if (!WillOverdraw)
                _overdrawAcknowledged = false;
        }
    }
    
    public string Description
    {
        get => _description;
        set => _description = value;
    }

    private async Task WithdrawFunds()
    {
        await _form.ValidateAsync();
        if (!_form.IsValid)
            return;
        var outcome = await RunAsync(
            async () => await TransactionService.AddTransaction(new AllowanceTransaction
            {
                Description = Description,
                TransactionAmount = -Amount,
                TenantId = Child.TenantId,
                ChildId = Child.Id,
                TransactionType = TransactionType.Withdrawal,
                RequestId = _requestId
            }, CancellationToken),
            successMessage: $"Withdrew {Amount:C2} from {Child.Name}.");

        if (outcome.Succeeded)
            MudDialog.Close();
    }
}
