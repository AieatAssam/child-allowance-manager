using Bunit;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Components.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace ChildAllowanceManager.Tests;

public class FamilySwitcherTests
{
    [Fact]
    public async Task One_family_is_shown_without_a_switcher_menu()
    {
        await using var context = CreateContext(out var tenants, out _);
        tenants.Tenants.Add(new TenantConfiguration
        {
            Id = "tenant-1", TenantName = "The Lovelace Family", UrlSuffix = "lovelace"
        });

        var cut = context.Render<FamilySwitcher>();

        cut.WaitForAssertion(() => Assert.Contains("The Lovelace Family", cut.Markup));
        Assert.DoesNotContain("Switch family", cut.Markup);
    }

    [Fact]
    public async Task Multiple_families_are_listed_in_the_switcher()
    {
        await using var context = CreateContext(out var tenants, out _);
        tenants.Tenants.AddRange([
            new TenantConfiguration { Id = "tenant-1", TenantName = "Alpha", UrlSuffix = "alpha" },
            new TenantConfiguration { Id = "tenant-2", TenantName = "Beta", UrlSuffix = "beta" }
        ]);
        var cut = context.Render<FamilySwitcher>();
        cut.WaitForAssertion(() => Assert.Contains("Switch family", cut.Markup));
        Assert.Contains("aria-label=\"Switch family\"", cut.Markup);
    }

    [Fact]
    public async Task Current_route_shows_the_active_family()
    {
        await using var context = CreateContext(out var tenants, out _);
        tenants.Tenants.AddRange([
            new TenantConfiguration { Id = "tenant-1", TenantName = "Alpha", UrlSuffix = "alpha" },
            new TenantConfiguration { Id = "tenant-2", TenantName = "Beta", UrlSuffix = "beta" }
        ]);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("http://localhost/beta/children");

        var cut = context.Render<FamilySwitcher>();
        cut.WaitForAssertion(() => Assert.Contains("Beta", cut.Markup));
        Assert.Equal("http://localhost/beta/children", navigation.Uri);
    }

    private static BunitContext CreateContext(
        out RecordingTenantService tenants, out RecordingCurrentContextService currentContext)
    {
        var context = BunitTestContext.Create();
        context.Services.AddDataProtection();
        context.Services.AddSingleton<ProtectedLocalStorage>(services => new ProtectedLocalStorage(
            context.JSInterop.JSRuntime, services.GetRequiredService<IDataProtectionProvider>()));
        tenants = new RecordingTenantService();
        currentContext = new RecordingCurrentContextService();
        context.Services.AddSingleton<ITenantService>(tenants);
        context.Services.AddSingleton<ICurrentContextService>(currentContext);
        return context;
    }
}
