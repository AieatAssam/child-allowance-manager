using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace ChildAllowanceManager.Components.Pages;

public partial class AddParentDialogue : CancellableComponentBase
{
    [CascadingParameter] private MudDialogInstance MudDialog { get; set; } = default!;
    [Parameter] public string TenantId { get; set; } = default!;
    
    [Inject] private IUserService UserService { get; set; } = default!;
    
    private User NewParent { get; set; } = new User();
    private MudForm _form;

    private async Task AddParentAsync()
    {
        await _form.Validate();
        if (!_form.IsValid)
            return;
        var result = await UserService.AddUserToTenantAsync(NewParent.Email, NewParent.Name, TenantId,
            ValidRoles.Parent, CancellationToken);
        MudDialog.Close(DialogResult.Ok(result));
    }
}