using System.Security.Cryptography;
using ChildAllowanceManager.Common.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;

namespace ChildAllowanceManager.Components.Layout;

public partial class MainLayout : IAsyncDisposable
{
    private bool _drawerOpen = false;
    private bool _useDarkMode;
    private IJSObjectReference? _jsModule;
    private MudThemeProvider _themeProvider = default!;
    private ErrorBoundary? _errorBoundary;
    
    private ThemeConfiguration _themeConfiguration = new ThemeConfiguration();

    [Inject] private ICurrentContextService CurrentContextService { get; set; } = default!;
    [Inject] private ProtectedLocalStorage LocalStorage { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ILogger<MainLayout> Logger { get; set; } = default!;

    private void DrawerToggle()
    {
        _drawerOpen = !_drawerOpen;
    }

    private void RecoverFromError() => _errorBoundary?.Recover();

    protected override void OnParametersSet() => _errorBoundary?.Recover();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Browser storage is not always reachable - a third-party iframe (the app embedded
            // in a Home Assistant dashboard), private browsing, or a kiosk with site data
            // blocked all make localStorage throw. An exception escaping OnAfterRenderAsync
            // tears the circuit down and the user gets a blank page, so remembering the last
            // family is best-effort. Everything the page needs comes from the URL.
            try
            {
                if (await LocalStorage.GetAsync<string>("current_tenant") is { Success: true } currentTenant)
                {
                    CurrentContextService.SetCurrentTenant(currentTenant.Value!);

                    // set long lived cookie
                    // load JS module once
                    _jsModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
                        "import", "./Components/Layout/MainLayout.razor.js?v=2");
                    // set cookie
                    await _jsModule.InvokeVoidAsync("createCookie", "current_tenant", currentTenant.Value!, 365);
                }
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException or CryptographicException)
            {
                Logger.LogDebug(ex, "Browser storage is unavailable; continuing without a remembered family.");
            }
        }

        if (firstRender)
        {
            // system light/dark theme support,
            // based on example from https://crispycode.net/exploring-the-mudthemeprovider-in-mudblazor/
            try
            {
                _useDarkMode = await _themeProvider.GetSystemDarkModeAsync();
                _themeConfiguration.IsDarkMode = _useDarkMode;
                await _themeProvider.WatchSystemDarkModeAsync(OnSystemPreferenceChanged);
                StateHasChanged();
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException)
            {
                Logger.LogDebug(ex, "System theme detection is unavailable; using the default theme.");
            }
        }
    }
    
    private Task OnSystemPreferenceChanged(bool newValue)
    {
        _useDarkMode = newValue;
        _themeConfiguration.IsDarkMode = _useDarkMode;
        StateHasChanged();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_jsModule is not null)
        {
            try { await _jsModule.DisposeAsync(); }
            catch (JSDisconnectedException) { /* circuit already gone */ }
        }
    }
}
