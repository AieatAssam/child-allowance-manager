using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChildAllowanceManager.Components.Pages;

public partial class AddHoldDialogue : CancellableComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;
    
    [Parameter] public ChildWithBalance Child { get; set; } = default!;
    
    [Inject] private IChildService ChildService { get; set; } = default!;
    
    public int Days { get; set; } = 1;
    
    public string Description { get; set; } = string.Empty;

    private MudForm _form = null!;
    private readonly string _requestId = Guid.NewGuid().ToString("N");
    
    private async Task AddHold()
    {
        await _form.ValidateAsync();
        if (!_form.IsValid)
            return;
        var outcome = await RunAsync(
            async () => await ChildService.ApplyHoldAsync(
                Child.Id, Child.TenantId, Days, Description, _requestId, CancellationToken),
            successMessage: $"Allowance paused for {Days} day(s).");

        if (outcome.Succeeded)
            MudDialog.Close();
    }
}
