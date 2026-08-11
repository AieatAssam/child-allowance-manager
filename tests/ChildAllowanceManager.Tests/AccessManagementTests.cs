using System.Security.Claims;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Services;
using DataAnnotationsValidationException = System.ComponentModel.DataAnnotations.ValidationException;
using Microsoft.EntityFrameworkCore;

namespace ChildAllowanceManager.Tests;

public class AccessManagementTests
{
    [Fact]
    public async Task Membership_queries_hide_revoked_access()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        db.Tenants.Add(Tenant());
        var users = new UserService(db, new MembershipService(db));
        var user = await users.InitializeUserAsync("person@example.com", "Person", "tenant-1", default);
        await users.AddUserToTenantAsync("second@example.com", "Second", "tenant-1", ValidRoles.Parent, default);
        var memberships = new MembershipService(db);

        Assert.Single(await memberships.GetMembershipsForUserAsync(user.Id));
        Assert.True(await memberships.RevokeAsync(user.Id, "tenant-1"));
        Assert.Empty(await memberships.GetMembershipsForUserAsync(user.Id));
        Assert.Null(await memberships.GetRoleAsync(user.Id, "tenant-1"));
    }

    [Fact]
    public void Authorization_is_scoped_to_the_selected_family_and_role()
    {
        var service = new TenantAuthorizationService();
        var parent = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(CustomClaimTypes.Tenant, "tenant-1"),
            new Claim(CustomClaimTypes.TenantRole, TenantRoleClaim.Format("tenant-1", ValidRoles.Parent))
        ], "test"));

        Assert.True(service.CanView(parent, "tenant-1"));
        Assert.False(service.CanView(parent, "tenant-2"));
        Assert.True(service.CanManage(parent, "tenant-1"));
        Assert.False(service.CanManage(parent, "tenant-2"));
    }

    [Fact]
    public async Task Invalid_invitation_email_is_rejected()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        db.Tenants.Add(Tenant());
        await db.SaveChangesAsync();
        var service = new InvitationService(
            db, new UserService(db, new MembershipService(db)), new MembershipService(db));

        await Assert.ThrowsAsync<DataAnnotationsValidationException>(() =>
            service.InviteAsync("tenant-1", "not-an-email", ValidRoles.Parent).AsTask());
    }

    [Fact]
    public async Task Pending_invitation_is_normalized_and_expires_after_fourteen_days()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        db.Tenants.Add(Tenant());
        await db.SaveChangesAsync();
        var service = new InvitationService(
            db, new UserService(db, new MembershipService(db)), new MembershipService(db));

        var invitation = await service.InviteAsync(
            "tenant-1", "  Person@Example.com ", ValidRoles.Parent);

        Assert.Equal("person@example.com", invitation.Email);
        Assert.InRange(invitation.ExpiresAt - DateTimeOffset.UtcNow,
            TimeSpan.FromDays(13.9), TimeSpan.FromDays(14.1));
        Assert.Single(await service.GetPendingForTenantAsync("tenant-1"));
    }

    [Fact]
    public async Task Accepting_pending_invitations_creates_membership_and_marks_them_accepted()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        db.Tenants.Add(Tenant());
        await db.SaveChangesAsync();
        var memberships = new MembershipService(db);
        var users = new UserService(db, memberships);
        var service = new InvitationService(db, users, memberships);
        await service.InviteAsync("tenant-1", "person@example.com", ValidRoles.Parent);

        Assert.Equal(1, await service.AcceptPendingAsync("PERSON@EXAMPLE.COM", "Person", default));

        var user = await users.GetUserByEmailAsync("person@example.com", default);
        Assert.NotNull(user);
        Assert.Equal(ValidRoles.Parent, await memberships.GetRoleAsync(user!.Id, "tenant-1"));
        Assert.Empty(await service.GetPendingForEmailAsync("person@example.com"));
        var storedInvitation = await db.TenantInvitations.SingleAsync();
        Assert.NotNull(storedInvitation.AcceptedAt);
    }

    [Fact]
    public async Task Revoking_the_last_parent_is_rejected()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        db.Tenants.Add(Tenant());
        await db.SaveChangesAsync();
        var memberships = new MembershipService(db);
        var users = new UserService(db, memberships);
        var user = await users.InitializeUserAsync("parent@example.com", "Parent", "tenant-1", default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            memberships.RevokeAsync(user.Id, "tenant-1").AsTask());
        Assert.Equal(ValidRoles.Parent, await memberships.GetRoleAsync(user.Id, "tenant-1"));
    }

    private static TenantConfiguration Tenant() => new()
    {
        Id = "tenant-1",
        TenantName = "Family",
        UrlSuffix = "family"
    };
}
