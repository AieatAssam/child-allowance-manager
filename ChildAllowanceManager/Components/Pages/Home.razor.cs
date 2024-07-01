using ChildAllowanceManager.Common.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace ChildAllowanceManager.Components.Pages;

public partial class Home : ComponentBase
{
    [Inject]
    public NavigationManager Navigation { get; set; } = default!;
    
    [Inject]
    public ProtectedLocalStorage LocalStorage { get; set; } = default!;
    
    [Inject]
    public ILogger<Home> Logger { get; set; } = default!;
    
    [Inject]
    protected IDataService DataService { get; set; } = default!;

    private bool _initialised = false;
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            _initialised = true;
        if (firstRender && await LocalStorage.GetAsync<string>("current_tenant") is { Success: true } currentTenant)
        {
            // get tenant
            var tenant = await DataService.GetTenant(currentTenant.Value!);
            Logger.LogInformation("Navigating to /{Tenant}/children", tenant.UrlSuffix);
            Navigation.NavigateTo($"/{tenant.UrlSuffix}/children");
        }
    }
}