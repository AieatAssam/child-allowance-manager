
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;


namespace ChildAllowanceManager.Components.Layout;

public partial class NavMenu
{
    [Inject] public ICurrentContextService CurrentContext { get; set; } = default!;

    [Inject] ProtectedLocalStorage LocalStorage { get; set; } = default!;

    public string? TenantSuffix { get; set; }
    private Dictionary<string, string> _cachedTenants = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender && await LocalStorage.GetAsync<string>("current_tenant") is var currentTenant &&
            currentTenant.Success)
        {
            var tenantId = currentTenant.Value;
            CurrentContext.SetCurrentTenant(tenantId);
            if (_cachedTenants.TryGetValue(tenantId, out var suffix))
            {
                TenantSuffix = suffix;
            }
            else
            {
                suffix = await CurrentContext.GetCurrentTenantSuffix();
                _cachedTenants.Add(tenantId, suffix!);
                TenantSuffix = suffix;
            }

            StateHasChanged();
        }
    }
}
    
