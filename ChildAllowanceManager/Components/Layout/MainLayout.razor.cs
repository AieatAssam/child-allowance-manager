using System.Security.Claims;
using ChildAllowanceManager.Common.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;
using MudBlazor;

namespace ChildAllowanceManager.Components.Layout;

public partial class MainLayout
{
    private bool _drawerOpen = false;
    private ClaimsPrincipal? _user;
    private bool _useDarkMode;
    private MudThemeProvider _themeProvider = default!;

    [CascadingParameter] private Task<AuthenticationState>? AuthenticationState { get; set; }

    [Inject] private ICurrentContextService CurrentContextService { get; set; } = default!;
    [Inject] private ProtectedLocalStorage LocalStorage { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private void DrawerToggle()
    {
        _drawerOpen = !_drawerOpen;
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        if (AuthenticationState != null)
        {
            var authState = await AuthenticationState;
            _user = authState.User;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && await LocalStorage.GetAsync<string>("current_tenant") is var currentTenant &&
            currentTenant.Success)
        {
            CurrentContextService.SetCurrentTenant(currentTenant.Value!);

            // set long lived cookie
            // load JS module
            var module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./Components/Layout/MainLayout.razor.js");
            // set cookie
            await JSRuntime.InvokeVoidAsync("MainLayout.createCookie", "current_tenant", currentTenant.Value!, 365);
        }

        if (firstRender)
        {
            // system light/dark theme support,
            // based on example from https://crispycode.net/exploring-the-mudthemeprovider-in-mudblazor/
            _useDarkMode = await _themeProvider.GetSystemPreference();
            await _themeProvider.WatchSystemPreference(OnSystemPreferenceChanged);
            StateHasChanged();
        }
    }
    
    private Task OnSystemPreferenceChanged(bool newValue)
    {
        _useDarkMode = newValue;
        StateHasChanged();
        return Task.CompletedTask;
    }
}