using System.ComponentModel.DataAnnotations;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Common.Validators;
using ChildAllowanceManager.Components.Pages;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChildAllowanceManager.Components;

public partial class TenantConfigurationEditor : CancellableComponentBase
{
    [Parameter, Required]
    public TenantConfiguration Tenant { get; set; } = default!;
    
    [Parameter]
    public EventCallback<TenantConfiguration> TenantChanged { get; set; }

    [Parameter] 
    public bool ReadOnly { get; set; } = false;
    
    [Inject]
    public IUserService UserService { get; set; } = default!;
    
    [Inject] IDialogService DialogService { get; set; } = default!;
    
    public readonly TenantConfigurationValidator Validator = new();

    private static readonly TimeZoneInfo[] TimeZones = TimeZoneInfo.GetSystemTimeZones()
        .OrderBy(timeZone => timeZone.BaseUtcOffset)
        .ThenBy(timeZone => timeZone.DisplayName)
        .ToArray();
    
    private MudForm? _form;
    private List<User> _parents = new();
    
    private async Task OnTenantChanged()
    {
        await (_form?.ValidateAsync() ?? Task.CompletedTask);
        if (_form?.IsValid ?? false)
        {
            await TenantChanged.InvokeAsync(Tenant);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (Tenant is not null && !ReadOnly)
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
            parent.Tenants = parent.Tenants.Where(id => id != Tenant.Id).ToArray();
            if (parent.Tenants.Length == 0)
                parent.Roles = parent.Roles.Where(r => r != ValidRoles.Parent).ToArray();
            await UserService.UpsertUserAsync(parent, CancellationToken.None);
            await ReloadParentsAsync();
        }
    }
    
    private async Task AddParentDialog()
    {
        if (Tenant is null)
            return;
        var options = new DialogOptions { CloseOnEscapeKey = true };
        DialogParameters<AddParentDialog> parameters = new();
        parameters.Add(d => d.TenantId, Tenant.Id);
        var dialog = await DialogService.ShowAsync<AddParentDialog>(null, parameters: parameters, options: options);
        var dialogResult = await dialog.Result;
        if (dialogResult is not null && !dialogResult.Canceled)
        {
            //var parent = dialogResult.Data as User;
            await ReloadParentsAsync();
        }
    }
}
