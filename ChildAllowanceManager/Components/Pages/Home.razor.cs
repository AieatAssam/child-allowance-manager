using System.Security.Cryptography;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Common.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;

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
    private string? _loadError;
    private TenantConfiguration[] _tenants = [];
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        await InitialiseAsync();
    }

    private async Task InitialiseAsync()
    {
        var outcome = await RunAsync(async () =>
        {
            var user = AuthenticationState is null ? null : (await AuthenticationState).User;
            _authenticated = user?.Identity?.IsAuthenticated == true;
            if (!_authenticated)
            {
                _initialised = true;
                return;
            }

            string? storedSuffix = null;
            try
            {
                var stored = await LocalStorage.GetAsync<string>("current_tenant_suffix");
                if (stored.Success)
                    storedSuffix = stored.Value;
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException or CryptographicException)
            {
                Logger.LogDebug(ex, "Remembered family is unavailable; continuing with the available families.");
            }

            _tenants = (await TenantService.GetTenantsForUser(user!, CancellationToken)).ToArray();
            if (storedSuffix is not null)
            {
                var selected = _tenants.FirstOrDefault(x => x.UrlSuffix == storedSuffix);
                if (selected is not null)
                {
                    Logger.LogInformation("Navigating to /{Tenant}/children", selected.UrlSuffix);
                    Navigation.NavigateTo($"/{selected.UrlSuffix}/children");
                    return;
                }

                _storedFamilyWasStale = true;
                try
                {
                    await LocalStorage.DeleteAsync("current_tenant");
                    await LocalStorage.DeleteAsync("current_tenant_suffix");
                }
                catch (Exception ex) when (ex is JSException or InvalidOperationException or CryptographicException)
                {
                    Logger.LogDebug(ex, "Could not clear the unavailable remembered family.");
                }
            }

            _initialised = true;
            if (_tenants.Length == 1)
                Navigation.NavigateTo($"/{_tenants[0].UrlSuffix}/children");
        }, successMessage: null);

        if (!outcome.Succeeded)
        {
            _loadError = "We couldn’t load your families.";
            _initialised = true;
            StateHasChanged();
        }
    }

    private async Task RetryAsync()
    {
        _loadError = null;
        _initialised = false;
        StateHasChanged();
        await InitialiseAsync();
    }
}
