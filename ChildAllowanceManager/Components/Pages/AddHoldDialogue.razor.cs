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
    
    private async Task AddHold()
    {
        await _form.ValidateAsync();
        if (!_form.IsValid)
            return;
        await ChildService.ApplyHoldAsync(Child.Id, Child.TenantId, Days, Description, null, CancellationToken);
        MudDialog.Close();
    }
}
