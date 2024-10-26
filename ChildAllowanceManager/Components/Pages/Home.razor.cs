using ChildAllowanceManager.Common.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace ChildAllowanceManager.Components.Pages;

public partial class Home : CancellableComponentBase
{
    [Inject]
    public NavigationManager Navigation { get; set; } = default!;
    
    [Inject]
    public ProtectedLocalStorage LocalStorage { get; set; } = default!;
    
    [Inject]
    public ILogger<Home> Logger { get; set; } = default!;
    
    [Inject]
    protected ITenantService TenantService { get; set; } = default!;

    private bool _initialised = false;
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            _initialised = true;
        if (firstRender && await LocalStorage.GetAsync<string>("current_tenant") is { Success: true } currentTenant)
        {
            // get tenant
            var tenant = await TenantService.GetTenant(currentTenant.Value!);
            if (tenant != null)
            {
                Logger.LogInformation("Navigating to /{Tenant}/children", tenant.UrlSuffix);
                Navigation.NavigateTo($"/{tenant.UrlSuffix}/children");
            }
        }
    }
}