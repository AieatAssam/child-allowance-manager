using System.Security.Claims;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Data;
using ChildAllowanceManager.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChildAllowanceManager.Tests;

public class AccountAndTenantServiceTests
{
    [Fact]
    public async Task FirstUserIsAdminAndTenantMembershipIsIdempotent()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var service = new UserService(db, new MembershipService(db));

        var first = await service.InitializeUserAsync(" Parent@Example.COM ", "Parent", "tenant-1", default);
        await service.AddUserToTenantAsync("parent@example.com", "Parent Updated", "tenant-1", ValidRoles.Parent, default);

        var stored = await service.GetUserByEmailAsync("parent@example.com", default);
        Assert.Contains(ValidRoles.Admin, first.Roles);
        Assert.Equal("parent@example.com", stored!.Email);
        Assert.Equal(["tenant-1"], stored.Tenants);
        Assert.Contains(ValidRoles.Parent, stored.Roles);
        Assert.Equal("Parent Updated", stored.Name);
    }

    [Fact]
    public async Task DeletingTenantSoftDeletesChildrenAndRemovesUserMembership()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var notifications = new GlobalNotificationService();
        var transactions = new TransactionService(db, notifications);
        var childService = new ChildService(db, notifications, transactions, NullLogger<ChildService>.Instance);
        var tenants = new TenantService(db, NullLogger<TenantService>.Instance);
        var users = new UserService(db, new MembershipService(db));
        var tenant = await tenants.AddTenant(new TenantConfiguration { TenantName = "Family", UrlSuffix = "family" });
        var child = await childService.AddChild(new ChildConfiguration
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            TenantId = tenant.Id
        });
        await users.InitializeUserAsync("parent@example.com", "Parent", tenant.Id, default);

        Assert.True(await tenants.DeleteTenant(tenant.Id));

        Assert.Null(await tenants.GetTenant(tenant.Id));
        Assert.Null(await childService.GetChild(child.Id, tenant.Id));
        Assert.Empty((await users.GetUserByEmailAsync("parent@example.com", default))!.Tenants);
        var deletedChild = await db.Children.FindAsync(child.Id);
        Assert.NotNull(deletedChild);
        Assert.True(deletedChild.Deleted);
    }

    [Fact]
    public async Task TenantSuffixesAreCaseInsensitiveAndMustBeUnique()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var service = new TenantService(
            db,
            NullLogger<TenantService>.Instance);
        await service.AddTenant(new TenantConfiguration { TenantName = "First Family", UrlSuffix = "family" });

        Assert.NotNull(await service.GetTenantBySuffix("FAMILY"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddTenant(new TenantConfiguration { TenantName = "Second Family", UrlSuffix = "family" }).AsTask());
    }

    [Fact]
    public async Task ClaimsTransformationRemovesRevokedAccess()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var userService = new UserService(db, new MembershipService(db));
        await userService.UpsertUserAsync(new User
        {
            Email = "parent@example.com",
            Roles = [ValidRoles.Parent],
            Tenants = ["tenant-2"]
        }, default);

        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Email, "parent@example.com"),
            new Claim(ClaimTypes.Role, ValidRoles.Admin),
            new Claim(CustomClaimTypes.Tenant, "tenant-1")
        ], "test"));

        var transformed = await new ClaimEnrichmentTransformer(
            userService,
            new MembershipService(db),
            NullLogger<ClaimEnrichmentTransformer>.Instance).TransformAsync(principal);

        Assert.DoesNotContain(transformed.Claims, c => c.Type == ClaimTypes.Role && c.Value == ValidRoles.Admin);
        Assert.DoesNotContain(transformed.Claims, c => c.Type == CustomClaimTypes.Tenant && c.Value == "tenant-1");
        Assert.Contains(transformed.Claims, c => c.Type == ClaimTypes.Role && c.Value == ValidRoles.Parent);
        Assert.Contains(transformed.Claims, c => c.Type == CustomClaimTypes.Tenant && c.Value == "tenant-2");
    }
}
