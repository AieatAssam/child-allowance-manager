using ChildAllowanceManager.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace ChildAllowanceManager.Tests;

public class SchemaConstraintTests
{
    [Fact]
    public async Task Transaction_with_unknown_child_is_rejected_by_the_database()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateMigratedContextAsync(cancellationToken);
        db.Tenants.Add(Tenant("tenant-fk"));
        db.Transactions.Add(Transaction("missing-child", "tenant-fk"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(cancellationToken));
    }

    [Fact]
    public async Task Child_with_unknown_tenant_is_rejected_by_the_database()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateMigratedContextAsync(cancellationToken);
        db.Children.Add(new ChildConfiguration { FirstName = "A", LastName = "B", TenantId = "missing-tenant" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(cancellationToken));
    }

    [Fact]
    public async Task Deleted_tenant_suffix_can_be_reused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateMigratedContextAsync(cancellationToken);
        db.Tenants.Add(Tenant("reuse", deleted: true));
        await db.SaveChangesAsync(cancellationToken);
        db.Tenants.Add(Tenant("reuse"));
        await db.SaveChangesAsync(cancellationToken);
    }

    [Fact]
    public async Task Two_live_tenants_cannot_share_a_suffix()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateMigratedContextAsync(cancellationToken);
        db.Tenants.AddRange(Tenant("duplicate"), Tenant("duplicate"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(cancellationToken));
    }

    [Fact]
    public async Task Deleted_user_email_can_be_reused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateMigratedContextAsync(cancellationToken);
        db.Users.Add(User("same@example.com", deleted: true));
        await db.SaveChangesAsync(cancellationToken);
        db.Users.Add(User("same@example.com"));
        await db.SaveChangesAsync(cancellationToken);
    }

    [Fact]
    public async Task Duplicate_request_id_within_a_tenant_is_rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateMigratedContextAsync(cancellationToken);
        var tenant = Tenant("request");
        db.Tenants.Add(tenant);
        var child = new ChildConfiguration { FirstName = "A", LastName = "B", TenantId = tenant.Id };
        db.Children.Add(child);
        await db.SaveChangesAsync(cancellationToken);
        db.Transactions.AddRange(Transaction(child.Id, tenant.Id, "same"), Transaction(child.Id, tenant.Id, "same"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(cancellationToken));
    }

    [Fact]
    public async Task Null_request_ids_do_not_collide()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateMigratedContextAsync(cancellationToken);
        var tenant = Tenant("null-request");
        db.Tenants.Add(tenant);
        var child = new ChildConfiguration { FirstName = "A", LastName = "B", TenantId = tenant.Id };
        db.Children.Add(child);
        await db.SaveChangesAsync(cancellationToken);
        db.Transactions.AddRange(Enumerable.Range(0, 3).Select(_ => Transaction(child.Id, tenant.Id)));
        await db.SaveChangesAsync(cancellationToken);
    }

    [Fact]
    public async Task Membership_is_unique_per_user_and_tenant_while_live()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateMigratedContextAsync(cancellationToken);
        db.Users.Add(User("member@example.com"));
        db.Tenants.Add(Tenant("member"));
        await db.SaveChangesAsync(cancellationToken);
        db.TenantMemberships.AddRange(
            new TenantMembership { UserId = db.Users.Local.Single().Id, TenantId = db.Tenants.Local.Single().Id },
            new TenantMembership { UserId = db.Users.Local.Single().Id, TenantId = db.Tenants.Local.Single().Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(cancellationToken));
    }

    private static TenantConfiguration Tenant(string suffix, bool deleted = false) =>
        new() { TenantName = suffix, UrlSuffix = suffix, Deleted = deleted };

    private static User User(string email, bool deleted = false) =>
        new() { Email = email, Name = email, Deleted = deleted };

    private static AllowanceTransaction Transaction(string childId, string tenantId, string? requestId = null) =>
        new() { ChildId = childId, TenantId = tenantId, Description = "test", TransactionAmount = 1, Balance = 1, RequestId = requestId };
}
