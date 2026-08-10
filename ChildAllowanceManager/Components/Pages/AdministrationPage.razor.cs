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
    
    private TenantConfiguration NewTenant { get; set; } = new();
    private bool AddingTenant { get; set; } = false;
    private TenantConfiguration[] Tenants { get; set; } = [];
    
    private string? TenantBeingEditedId = null;
    private MudMessageBox DeleteTenantMessageBox { get; set; } = null!;

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
        if (true != await DeleteTenantMessageBox.ShowAsync())
        {
            return;
        }
        var outcome = await RunAsync(
            async () => await TenantService.DeleteTenant(tenant.Id, CancellationToken),
            successMessage: "Family removed. You can restore it from the deleted list.");
        if (outcome.Succeeded)
            await ReloadTenants();
    }

    private async Task ReloadTenants()
    {
        TenantConfiguration[]? loaded = null;
        var outcome = await RunAsync(
            async () => loaded = (await TenantService.GetTenants(CancellationToken)).ToArray());
        if (!outcome.Succeeded)
            return;

        Tenants = loaded!;
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
