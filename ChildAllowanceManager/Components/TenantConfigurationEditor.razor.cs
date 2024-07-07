using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Common.Validators;
using ChildAllowanceManager.Components.Pages;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChildAllowanceManager.Components;

public partial class TenantConfigurationEditor : CancellableComponentBase
{
    [Parameter]
    public TenantConfiguration? Tenant { get; set; }
    
    [Parameter]
    public EventCallback<TenantConfiguration> TenantChanged { get; set; }

    [Parameter] 
    public bool ReadOnly { get; set; } = false;
    
    [Inject]
    public IUserService UserService { get; set; } = default!;
    
    [Inject] IDialogService DialogService { get; set; } = default!;
    
    public readonly TenantConfigurationValidator Validator = new();
    
    private MudForm? _form;
    private List<User> _parents = new();
    
    private async Task OnTenantChanged()
    {
        await (_form?.Validate() ?? Task.CompletedTask);
        if (_form?.IsValid ?? false)
        {
            await TenantChanged.InvokeAsync(Tenant);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (Tenant is not null)
        {
            await ReloadParentsAsync();
        }
    }

    private async Task ReloadParentsAsync()
    {
        if (Tenant is null)
            return;
        var parents = await UserService.GetTenantUsersInRole(Tenant.Id, ValidRoles.Parent, CancellationToken.None);
        _parents = parents.ToList();
        StateHasChanged();
    }

    private async Task RemoveParentAsync(MudChip<string> chip)
    {
        var parent = _parents.FirstOrDefault(p => p.Id == chip.Value);
        if (parent is not null)
        {
            _parents.Remove(parent);
            parent.Roles = parent.Roles.Where(r => r != ValidRoles.Parent).ToArray();
            await UserService.UpsertUserAsync(parent, CancellationToken.None);
            await ReloadParentsAsync();
        }
    }
    
    private async Task AddParentAsync(string email, string name)
    {
        if (Tenant is null)
            return;
        var parent = await UserService.GetUserByEmailAsync(email, CancellationToken.None);
        if (parent is null)
            parent = await UserService.InitializeUserAsync(email, name, Tenant.Id, CancellationToken.None);
        _parents.Remove(parent);
        parent.Roles = parent.Roles.Where(r => r != ValidRoles.Parent).ToArray();
        await UserService.UpsertUserAsync(parent, CancellationToken.None);
        await ReloadParentsAsync();
    }
    
    private async Task AddParentDialogue()
    {
        if (Tenant is null)
            return;
        var options = new DialogOptions { CloseOnEscapeKey = true };
        DialogParameters<AddParentDialogue> parameters = new();
        parameters.Add(d => d.TenantId, Tenant.Id);
        var dialogue = await DialogService.ShowAsync<AddParentDialogue>(null, parameters: parameters, options: options);
        var dialogueResult = await dialogue.Result;
        if (dialogueResult is not null && !dialogueResult.Canceled)
        {
            //var parent = dialogueResult.Data as User; 
            await ReloadParentsAsync();
        }
    }
}