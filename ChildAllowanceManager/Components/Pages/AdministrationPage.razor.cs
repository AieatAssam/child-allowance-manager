using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChildAllowanceManager.Components.Pages;

public partial class AdministrationPage : ComponentBase
{
    [Inject]
    private IDataService DataService { get; set; } = default!;
    
    private TenantConfiguration NewTenant { get; set; } = new TenantConfiguration();
    private bool AddingTenant { get; set; } = false;
    private TenantConfiguration[] Tenants { get; set; } = null;
    
    private string? TenantBeingEditedId = null;
    private MudMessageBox DeleteTenantMessageBox { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        await ReloadTenants();
    }

    

    private async Task AddTenant()
    {
        await DataService.AddTenant(NewTenant, CancellationToken.None);
        await ReloadTenants();
    }

    private async Task DeleteTenant(TenantConfiguration tenant)
    {
        if (true != await DeleteTenantMessageBox.ShowAsync())
        {
            return;
        }
        await DataService.DeleteTenant(tenant.Id, CancellationToken.None);
        await ReloadTenants();
    }

    private async Task ReloadTenants()
    {
        Tenants = (await DataService.GetTenants(CancellationToken.None)).ToArray();
        TenantBeingEditedId = null;
        AddingTenant = false;
        NewTenant = new TenantConfiguration();
    }

    private async Task UpdateTenant(TenantConfiguration tenant)
    {
        await DataService.UpdateTenant(tenant, CancellationToken.None);
        await ReloadTenants();
    }
}