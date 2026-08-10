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
    protected IWebHostEnvironment Environment { get; set; } = default!;

    private bool _initialised = false;
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _initialised = true;
            StateHasChanged();
        }
        if (firstRender && await LocalStorage.GetAsync<string>("current_tenant_suffix") is { Success: true } currentTenant)
        {
            Logger.LogInformation("Navigating to /{Tenant}/children", currentTenant.Value);
            Navigation.NavigateTo($"/{currentTenant.Value}/children");
        }
    }
}
