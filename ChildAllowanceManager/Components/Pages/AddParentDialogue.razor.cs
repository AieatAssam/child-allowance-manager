using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChildAllowanceManager.Components.Pages;

public partial class AddParentDialogue : CancellableComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;
    [Parameter] public string TenantId { get; set; } = default!;
    
    [Inject] private IUserService UserService { get; set; } = default!;
    
    private User NewParent { get; set; } = new User();
    private MudForm _form = null!;

    private async Task AddParentAsync()
    {
        await _form.ValidateAsync();
        if (!_form.IsValid)
            return;
        var outcome = await RunAsync(
            async () => await UserService.AddUserToTenantAsync(
                NewParent.Email, NewParent.Name, TenantId, ValidRoles.Parent, CancellationToken),
            successMessage: "Parent added.");
        if (outcome.Succeeded)
            MudDialog.Close(DialogResult.Ok());
    }
}
