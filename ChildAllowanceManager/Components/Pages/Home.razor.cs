using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Common.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
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
    protected IWebHostEnvironment Environment { get; set; } = default!;

    [Inject]
    private ITenantService TenantService { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationState { get; set; }

    private bool _initialised = false;
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _initialised = true;
            StateHasChanged();
        }
        if (firstRender &&
            await LocalStorage.GetAsync<string>("current_tenant_suffix") is { Success: true } currentTenant &&
            await HasTenantAccessBySuffixAsync(currentTenant.Value!))
        {
            Logger.LogInformation("Navigating to /{Tenant}/children", currentTenant.Value);
            Navigation.NavigateTo($"/{currentTenant.Value}/children");
        }
    }

    private async Task<bool> HasTenantAccessBySuffixAsync(string suffix)
    {
        if (AuthenticationState is null)
            return true;
        var tenant = await TenantService.GetTenantBySuffix(suffix, CancellationToken);
        if (tenant is null)
            return false;
        var user = (await AuthenticationState).User;
        return user.IsInRole(ValidRoles.Admin) || user.HasClaim(CustomClaimTypes.Tenant, tenant.Id);
    }
}
