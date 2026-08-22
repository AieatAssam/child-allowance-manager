using System.Security.Claims;
using Bunit;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Components.Pages;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace ChildAllowanceManager.Tests;

public class ShareLinkUiTests
{
    [Fact]
    public async Task People_page_lists_share_links()
    {
        await using var context = CreateContext(out var shareService);
        shareService.Link = new ShareLink
        {
            Id = "link-1", TenantId = "tenant-1", Name = "Kitchen tablet",
            CreatedTimestamp = DateTimeOffset.UtcNow.AddDays(-2)
        };

        var cut = context.Render<PeoplePage>(parameters => parameters.Add(x => x.TenantSuffix, "family"));

        cut.WaitForAssertion(() => Assert.Contains("Kitchen tablet", cut.Markup));
        Assert.Contains("Display links", cut.Markup);
    }

    [Fact]
    public async Task People_page_shows_an_empty_state_when_there_are_none()
    {
        await using var context = CreateContext(out _);

        var cut = context.Render<PeoplePage>(parameters => parameters.Add(x => x.TenantSuffix, "family"));

        cut.WaitForAssertion(() => Assert.Contains("No display links yet.", cut.Markup));
    }

    [Fact]
    public async Task Create_dialog_reveals_the_url_once_and_does_not_close()
    {
        await using var context = CreateContext(out var shareService);
        var provider = context.Render<MudDialogProvider>();
        var cut = context.Render<PeoplePage>(parameters => parameters.Add(x => x.TenantSuffix, "family"));
        cut.WaitForAssertion(() => Assert.Contains("New display link", cut.Markup));
        cut.FindAll("button").Single(x => x.TextContent.Trim() == "New display link").Click();
        provider.WaitForAssertion(() => Assert.Contains("What is this link for?", provider.Markup));

        provider.FindAll("input").First().Change("Kitchen tablet");
        provider.FindAll("button").Single(x => x.TextContent.Trim() == "Make the link").Click();

        provider.WaitForAssertion(() => Assert.Contains("/share/test-share-token", provider.Markup));
        Assert.Equal(1, shareService.CreateCalls);
    }

    [Fact]
    public async Task Create_dialog_rejects_an_empty_name()
    {
        await using var context = CreateContext(out var shareService);
        var provider = context.Render<MudDialogProvider>();
        var cut = context.Render<PeoplePage>(parameters => parameters.Add(x => x.TenantSuffix, "family"));
        cut.WaitForAssertion(() => Assert.Contains("New display link", cut.Markup));
        cut.FindAll("button").Single(x => x.TextContent.Trim() == "New display link").Click();
        provider.WaitForAssertion(() => Assert.Contains("What is this link for?", provider.Markup));

        provider.FindAll("button").Single(x => x.TextContent.Trim() == "Make the link").Click();

        Assert.Equal(0, shareService.CreateCalls);
    }

    [Fact]
    public async Task Revoke_asks_for_confirmation_before_calling_the_service()
    {
        await using var context = CreateContext(out var shareService);
        shareService.Link = new ShareLink { Id = "link-1", TenantId = "tenant-1", Name = "Kitchen tablet" };
        var cut = context.Render<PeoplePage>(parameters => parameters.Add(x => x.TenantSuffix, "family"));
        cut.WaitForAssertion(() => Assert.Contains("Turn off", cut.Markup));

        cut.FindAll("button").Single(x => x.TextContent.Trim() == "Turn off").Click();

        Assert.Null(shareService.LastRevokeTenantId);
    }

    [Fact]
    public async Task Revoke_passes_the_current_tenant_id()
    {
        await using var context = CreateContext(out var shareService);
        shareService.Link = new ShareLink { Id = "link-1", TenantId = "tenant-1", Name = "Kitchen tablet" };
        var provider = context.Render<MudDialogProvider>();
        var cut = context.Render<PeoplePage>(parameters => parameters.Add(x => x.TenantSuffix, "family"));
        cut.WaitForAssertion(() => Assert.Contains("Turn off", cut.Markup));
        cut.FindAll("button").Single(x => x.TextContent.Trim() == "Turn off").Click();
        provider.FindAll("button").Single(x => x.TextContent.Trim() == "Confirm").Click();

        Assert.Equal("tenant-1", shareService.LastRevokeTenantId);
    }

    private static BunitContext CreateContext(out RecordingShareLinkService shareService)
    {
        var context = BunitTestContext.Create();
        var tenantService = new RecordingTenantService();
        tenantService.Tenants.Add(new TenantConfiguration
        {
            Id = "tenant-1", TenantName = "Family", UrlSuffix = "family"
        });
        shareService = new RecordingShareLinkService();
        context.Services.AddSingleton<ITenantService>(tenantService);
        context.Services.AddSingleton<IMembershipService>(new EmptyMembershipService());
        context.Services.AddSingleton<IShareLinkService>(shareService);
        context.Services.AddSingleton<IInvitationService>(new RecordingInvitationService());
        var auth = context.AddAuthorization();
        auth.SetAuthorized("Parent");
        auth.SetClaims(new Claim(CustomClaimTypes.TenantRole, TenantRoleClaim.Format("tenant-1", ValidRoles.Parent)));
        return context;
    }

    private sealed class EmptyMembershipService : IMembershipService
    {
        public ValueTask<IEnumerable<TenantMembership>> GetMembershipsForUserAsync(string userId, CancellationToken ct = default) =>
            ValueTask.FromResult<IEnumerable<TenantMembership>>([]);
        public ValueTask<IEnumerable<TenantMembership>> GetMembershipsByEmailAsync(string email, CancellationToken ct = default) =>
            ValueTask.FromResult<IEnumerable<TenantMembership>>([]);
        public ValueTask<IEnumerable<TenantMembership>> GetMembershipsForTenantAsync(string tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult<IEnumerable<TenantMembership>>([]);
        public ValueTask<TenantMembership> GrantAsync(string userId, string tenantId, string role, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public ValueTask<bool> RevokeAsync(string userId, string tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(false);
        public ValueTask<string?> GetRoleAsync(string userId, string tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult<string?>(null);
    }
}
