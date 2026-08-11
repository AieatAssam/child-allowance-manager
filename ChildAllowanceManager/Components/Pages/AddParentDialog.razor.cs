using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChildAllowanceManager.Components.Pages;

public partial class AddParentDialog : CancellableComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;
    [Parameter] public string TenantId { get; set; } = default!;
    
    [Inject] private IInvitationService InvitationService { get; set; } = default!;
    
    private User NewParent { get; set; } = new User();
    private MudForm _form = null!;

    private async Task AddParentAsync()
    {
        await _form.ValidateAsync();
        if (!_form.IsValid)
            return;
        var outcome = await RunAsync(
            async () => await InvitationService.InviteAsync(
                TenantId, NewParent.Email, ValidRoles.Parent, CancellationToken),
            successMessage: $"Invitation sent to {NewParent.Email}.");
        if (outcome.Succeeded)
            MudDialog.Close(DialogResult.Ok(true));
    }
}
