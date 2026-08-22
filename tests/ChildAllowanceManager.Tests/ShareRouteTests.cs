using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Components.Pages;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;

namespace ChildAllowanceManager.Tests;

public class ShareRouteTests
{
    [Fact]
    public async Task Share_route_renders_balances()
    {
        await using var context = CreateContext(out var childService, out var shareService);
        childService.Children.Add(new ChildConfiguration
        {
            Id = "child-1", TenantId = "tenant-1", FirstName = "Ada", LastName = "Lovelace"
        });
        shareService.Link = LiveLink();

        var cut = context.Render<ChildrenListPage>(parameters => parameters.Add(x => x.ShareToken, "token"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Ada Lovelace", cut.Markup);
            Assert.Contains("10.00", cut.Markup);
        });
    }

    [Fact]
    public async Task Share_route_renders_no_money_controls()
    {
        await using var context = CreateContext(out var childService, out var shareService);
        childService.Children.Add(new ChildConfiguration
        {
            Id = "child-1", TenantId = "tenant-1", FirstName = "Ada", LastName = "Lovelace",
            HoldDaysRemaining = 1
        });
        shareService.Link = LiveLink();

        var cut = context.Render<ChildrenListPage>(parameters => parameters.Add(x => x.ShareToken, "token"));
        cut.WaitForAssertion(() => Assert.Contains("Ada Lovelace", cut.Markup));

        Assert.DoesNotContain("Add money", cut.Markup);
        Assert.DoesNotContain("Withdraw", cut.Markup);
        Assert.DoesNotContain("Pause allowance", cut.Markup);
        Assert.DoesNotContain("Remove one day", cut.Markup);
    }

    [Fact]
    public async Task Share_route_still_renders_the_history_button()
    {
        await using var context = CreateContext(out var childService, out var shareService);
        childService.Children.Add(new ChildConfiguration
        {
            Id = "child-1", TenantId = "tenant-1", FirstName = "Ada", LastName = "Lovelace"
        });
        shareService.Link = LiveLink();

        var cut = context.Render<ChildrenListPage>(parameters => parameters.Add(x => x.ShareToken, "token"));

        cut.WaitForAssertion(() => Assert.Contains("History", cut.Markup));
    }

    [Fact]
    public async Task Share_route_with_an_unresolvable_token_navigates_to_share_expired()
    {
        await using var context = CreateContext(out _, out _);

        context.Render<ChildrenListPage>(parameters => parameters.Add(x => x.ShareToken, "token"));

        Assert.EndsWith("/share/expired", context.Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public async Task Share_route_does_not_write_local_storage()
    {
        await using var context = CreateContext(out var childService, out var shareService);
        childService.Children.Add(new ChildConfiguration
        {
            Id = "child-1", TenantId = "tenant-1", FirstName = "Ada", LastName = "Lovelace"
        });
        shareService.Link = LiveLink();
        var currentContext = new RecordingCurrentContextService();
        context.Services.AddSingleton<ICurrentContextService>(currentContext);

        context.Render<ChildrenListPage>(parameters => parameters.Add(x => x.ShareToken, "token"));

        Assert.Equal("tenant-1", currentContext.TenantId);
        Assert.DoesNotContain(context.JSInterop.Invocations,
            invocation => invocation.Identifier.Contains("localStorage.setItem", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Suffix_route_for_a_manager_still_renders_money_controls()
    {
        await using var context = CreateContext(out var childService, out var shareService);
        childService.Children.Add(new ChildConfiguration
        {
            Id = "child-1", TenantId = "tenant-1", FirstName = "Ada", LastName = "Lovelace"
        });
        context.Services.AddSingleton<ITenantService>(new RecordingTenantService
        {
            Tenants = { LiveLink().Tenant! }
        });
        var cut = context.Render<ChildrenListPage>(parameters => parameters.Add(x => x.TenantSuffix, "tenant-1"));

        cut.WaitForAssertion(() => Assert.Contains("Add money", cut.Markup));
        Assert.Contains("Withdraw", cut.Markup);
        Assert.Null(shareService.Link);
    }

    [Fact]
    public async Task Share_route_gives_a_signed_in_parent_of_that_family_their_controls()
    {
        // The link decides who may look. It never takes permissions away from someone
        // who already has them.
        await using var context = CreateContext(out var childService, out var shareService);
        childService.Children.Add(new ChildConfiguration
        {
            Id = "child-1", TenantId = "tenant-1", FirstName = "Ada", LastName = "Lovelace"
        });
        shareService.Link = LiveLink();
        context.Services.AddSingleton<AuthenticationStateProvider>(new FixedAuthenticationStateProvider(
            new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(CustomClaimTypes.Tenant, "tenant-1"),
                new Claim(CustomClaimTypes.TenantRole, TenantRoleClaim.Format("tenant-1", ValidRoles.Parent))
            ], "test"))));

        var cut = context.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<ChildrenListPage>(child => child.Add(x => x.ShareToken, "token")));

        cut.WaitForAssertion(() => Assert.Contains("Add money", cut.Markup));
        Assert.Contains("Withdraw", cut.Markup);
    }

    [Fact]
    public async Task Share_route_stays_read_only_for_a_signed_in_stranger()
    {
        await using var context = CreateContext(out var childService, out var shareService);
        childService.Children.Add(new ChildConfiguration
        {
            Id = "child-1", TenantId = "tenant-1", FirstName = "Ada", LastName = "Lovelace"
        });
        shareService.Link = LiveLink();
        context.Services.AddSingleton<AuthenticationStateProvider>(new FixedAuthenticationStateProvider(
            new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(CustomClaimTypes.Tenant, "other-tenant"),
                new Claim(CustomClaimTypes.TenantRole, TenantRoleClaim.Format("other-tenant", ValidRoles.Parent))
            ], "test"))));

        var cut = context.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<ChildrenListPage>(child => child.Add(x => x.ShareToken, "token")));

        cut.WaitForAssertion(() => Assert.Contains("Ada Lovelace", cut.Markup));
        Assert.DoesNotContain("Add money", cut.Markup);
        Assert.DoesNotContain("Withdraw", cut.Markup);
    }

    private sealed class FixedAuthenticationStateProvider(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(principal));
    }

    private static BunitContext CreateContext(
        out RecordingChildService childService, out RecordingShareLinkService shareService)
    {
        var context = BunitTestContext.Create();
        context.Services.AddDataProtection();
        context.Services.AddSingleton<ProtectedLocalStorage>(services =>
            new ProtectedLocalStorage(context.JSInterop.JSRuntime,
                services.GetRequiredService<IDataProtectionProvider>()));
        childService = new RecordingChildService();
        shareService = new RecordingShareLinkService();
        context.Services.AddSingleton<IChildService>(childService);
        context.Services.AddSingleton<IShareLinkService>(shareService);
        context.Services.AddSingleton<ITenantNotificationService>(new RecordingTenantNotificationService());
        context.Services.AddSingleton<ICurrentContextService>(new RecordingCurrentContextService());
        return context;
    }

    private static ShareLink LiveLink() => new()
    {
        Id = "link-1",
        TenantId = "tenant-1",
        Name = "Kitchen tablet",
        Tenant = new TenantConfiguration { Id = "tenant-1", TenantName = "Family", UrlSuffix = "tenant-1" }
    };
}
