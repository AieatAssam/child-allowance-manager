using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace ChildAllowanceManager.Components.Layout;

public partial class FamilySwitcher : CancellableComponentBase
{
    [Inject] private ITenantService TenantService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ICurrentContextService CurrentContextService { get; set; } = default!;
    [Inject] private ProtectedLocalStorage LocalStorage { get; set; } = default!;

    [CascadingParameter] private Task<AuthenticationState>? AuthenticationState { get; set; }

    private TenantConfiguration[] _tenants = [];
    private TenantConfiguration? _activeTenant;

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += OnLocationChanged;
        UpdateActiveTenant(Navigation.Uri);
    }

    protected override async Task OnInitializedAsync()
    {
        await RunAsync(async () =>
        {
            var principal = AuthenticationState is null
                ? new System.Security.Claims.ClaimsPrincipal()
                : (await AuthenticationState).User;
            _tenants = (await TenantService.GetTenantsForUser(principal, CancellationToken)).ToArray();
            UpdateActiveTenant(Navigation.Uri);
        }, successMessage: null);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        var previous = _activeTenant;
        UpdateActiveTenant(args.Location);
        if (previous?.Id != _activeTenant?.Id)
            _ = InvokeAsync(StateHasChanged);
    }

    private void UpdateActiveTenant(string uri)
    {
        var segments = Navigation.ToBaseRelativePath(uri)
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        var suffix = segments.Length == 2 && segments[1] is "children" or "configuration" or "people"
            ? segments[0]
            : null;
        _activeTenant = _tenants.FirstOrDefault(x => x.UrlSuffix == suffix);
    }

    private async Task SelectFamilyAsync(TenantConfiguration tenant)
    {
        await RunAsync(async () =>
        {
            await LocalStorage.SetAsync("current_tenant", tenant.Id);
            await LocalStorage.SetAsync("current_tenant_suffix", tenant.UrlSuffix);
            CurrentContextService.SetCurrentTenant(tenant.Id);
            Navigation.NavigateTo($"/{tenant.UrlSuffix}/children");
        }, successMessage: null);
    }

    public override void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        base.Dispose();
    }
}
