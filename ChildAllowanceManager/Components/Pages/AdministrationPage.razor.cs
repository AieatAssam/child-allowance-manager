using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChildAllowanceManager.Components.Pages;

public partial class AdministrationPage : CancellableComponentBase
{
    [Inject] 
    private ITenantService TenantService { get; set; } = default!;
    
    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IDialogService DialogService { get; set; } = default!;
    
    private TenantConfiguration NewTenant { get; set; } = new();
    private bool AddingTenant { get; set; } = false;
    private TenantConfiguration[] Tenants { get; set; } = [];
    private TenantConfiguration[] DeletedTenants { get; set; } = [];
    private bool IsLoading { get; set; } = true;
    private string? LoadError { get; set; }
    
    private string? TenantBeingEditedId = null;
    private void BeginAddingTenant()
    {
        TenantBeingEditedId = null;
        AddingTenant = true;
    }

    private void BeginEditingTenant(string tenantId)
    {
        AddingTenant = false;
        TenantBeingEditedId = tenantId;
    }

    private void BeginAddingTenant()
    {
        TenantBeingEditedId = null;
        AddingTenant = true;
    }

    private void BeginEditingTenant(string tenantId)
    {
        AddingTenant = false;
        TenantBeingEditedId = tenantId;
    }

    protected override async Task OnInitializedAsync()
    {
        await ReloadTenants();
    }

    

    private async Task AddTenant()
    {
        var outcome = await RunAsync(
            async () => await TenantService.AddTenant(NewTenant, CancellationToken),
            successMessage: "Family added.");
        if (outcome.Succeeded)
            await ReloadTenants();
    }

    private async Task DeleteTenant(TenantConfiguration tenant)
    {
        if (!await ConfirmDialog.ShowAsync(
                DialogService,
                "Warning",
                "Delete this family? This hides the family and its children. An administrator can restore it."))
        {
            return;
        }
        var outcome = await RunAsync(
            async () => await TenantService.DeleteTenant(tenant.Id, CancellationToken),
            successMessage: "Family removed. You can restore it from the deleted list.");
        if (outcome.Succeeded)
            await ReloadTenants();
    }

    private async Task RestoreTenant(TenantConfiguration tenant)
    {
        var outcome = await RunAsync(
            async () => await TenantService.RestoreTenant(tenant.Id, CancellationToken),
            successMessage: $"{tenant.TenantName} restored.");
        if (outcome.Succeeded)
            await ReloadTenants();
    }

    private async Task ReloadTenants()
    {
        IsLoading = true;
        LoadError = null;
        TenantConfiguration[]? loaded = null;
        TenantConfiguration[]? deleted = null;
        var outcome = await RunAsync(
            async () =>
            {
                loaded = (await TenantService.GetTenants(CancellationToken)).ToArray();
                deleted = (await TenantService.GetDeletedTenants(CancellationToken)).ToArray();
            });
        if (!outcome.Succeeded)
        {
            IsLoading = false;
            LoadError = outcome.ErrorMessage;
            return;
        }

        Tenants = loaded!;
        DeletedTenants = deleted!;
        IsLoading = false;
        TenantBeingEditedId = null;
        AddingTenant = false;
        NewTenant = new TenantConfiguration();
    }

    private async Task UpdateTenant(TenantConfiguration tenant)
    {
        var outcome = await RunAsync(
            async () => await TenantService.UpdateTenant(tenant, CancellationToken),
            successMessage: "Family updated.");
        if (outcome.Succeeded)
            await ReloadTenants();
    }
}
