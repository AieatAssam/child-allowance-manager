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
    private bool _authenticated;
    private bool _storedFamilyWasStale;
    private TenantConfiguration[] _tenants = [];
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        await RunAsync(async () =>
        {
            var user = AuthenticationState is null ? null : (await AuthenticationState).User;
            _authenticated = user?.Identity?.IsAuthenticated == true;
            if (!_authenticated)
            {
                _initialised = true;
                return;
            }

            var stored = await LocalStorage.GetAsync<string>("current_tenant_suffix");
            _tenants = (await TenantService.GetTenantsForUser(user!, CancellationToken)).ToArray();
            if (stored is { Success: true, Value: not null })
            {
                var selected = _tenants.FirstOrDefault(x => x.UrlSuffix == stored.Value);
                if (selected is not null)
                {
                    Logger.LogInformation("Navigating to /{Tenant}/children", selected.UrlSuffix);
                    Navigation.NavigateTo($"/{selected.UrlSuffix}/children");
                    return;
                }

                _storedFamilyWasStale = true;
                await LocalStorage.DeleteAsync("current_tenant");
                await LocalStorage.DeleteAsync("current_tenant_suffix");
            }

            _initialised = true;
            if (_tenants.Length == 1)
                Navigation.NavigateTo($"/{_tenants[0].UrlSuffix}/children");
        }, successMessage: null);
    }
}
