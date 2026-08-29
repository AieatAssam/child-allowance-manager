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
    private readonly SemaphoreSlim _parentsGate = new(1, 1);
    
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

        await _parentsGate.WaitAsync(CancellationToken);
        try
        {
            var tenantId = Tenant.Id;
            List<User>? parents = null;
            var outcome = await Operations.RunAsync(
                async () =>
                {
                    parents = (await UserService.GetTenantUsersInRole(
                        tenantId, ValidRoles.Parent, CancellationToken)).ToList();
                },
                cancellationToken: CancellationToken);
            if (outcome.Succeeded && Tenant?.Id == tenantId)
            {
                _parents = parents!;
                StateHasChanged();
            }
        }
        finally
        {
            _parentsGate.Release();
        }
    }

    private async Task RemoveParentAsync(MudChip<string> chip)
    {
        await _parentsGate.WaitAsync(CancellationToken);
        try
        {
            var parent = _parents.FirstOrDefault(p => p.Id == chip.Value);
            if (parent is null)
                return;

            parent.Tenants = parent.Tenants.Where(id => id != Tenant.Id).ToArray();
            if (parent.Tenants.Length == 0)
                parent.Roles = parent.Roles.Where(r => r != ValidRoles.Parent).ToArray();
            var outcome = await Operations.RunAsync(
                async () =>
                {
                    await UserService.UpsertUserAsync(parent, CancellationToken);
                },
                cancellationToken: CancellationToken);
            if (outcome.Succeeded)
                _parents.Remove(parent);
        }
        finally
        {
            _parentsGate.Release();
        }

        await ReloadParentsAsync();
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
