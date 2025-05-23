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
        await TenantService.AddTenant(NewTenant, CancellationToken);
        await ReloadTenants();
    }

    private async Task DeleteTenant(TenantConfiguration tenant)
    {
        if (true != await DeleteTenantMessageBox.ShowAsync())
        {
            return;
        }
        await TenantService.DeleteTenant(tenant.Id, CancellationToken);
        await ReloadTenants();
    }

    private async Task ReloadTenants()
    {
        Tenants = (await TenantService.GetTenants(CancellationToken)).ToArray();
        TenantBeingEditedId = null;
        AddingTenant = false;
        NewTenant = new TenantConfiguration();
    }

    private async Task UpdateTenant(TenantConfiguration tenant)
    {
        await TenantService.UpdateTenant(tenant, CancellationToken);
        await ReloadTenants();
    }
}