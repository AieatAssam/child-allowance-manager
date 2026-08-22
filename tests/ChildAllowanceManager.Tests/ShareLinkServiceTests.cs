using System.ComponentModel.DataAnnotations;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChildAllowanceManager.Tests;

public class ShareLinkServiceTests
{
    [Fact]
    public async Task CreateAsync_returns_a_token_that_is_not_persisted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await CreateDatabaseAsync();
        var result = await Service(db).CreateAsync("tenant-1", "Kitchen tablet", "parent@example.com", null, cancellationToken);

        var stored = db.ShareLinks.Single();
        Assert.NotEqual(result.Token, stored.TokenHash);
        Assert.DoesNotContain(result.Token, string.Join("|", new[]
        {
            stored.Id, stored.TenantId, stored.Name, stored.TokenHash, stored.CreatedByEmail,
            stored.ExpiresAt?.ToString(), stored.LastAccessedAt?.ToString()
        }));
    }

    [Fact]
    public async Task ResolveAsync_returns_the_link_for_a_live_token()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await CreateDatabaseAsync();
        var result = await Service(db).CreateAsync("tenant-1", "Tablet", "parent@example.com", null, cancellationToken);

        var resolved = await Service(db).ResolveAsync(result.Token, cancellationToken);

        Assert.Equal(result.Link.Id, resolved?.Id);
        Assert.NotNull(resolved?.LastAccessedAt);
    }

    [Fact]
    public async Task ResolveAsync_returns_null_for_an_unknown_token()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await CreateDatabaseAsync();

        Assert.Null(await Service(db).ResolveAsync("not-a-real-token", cancellationToken));
    }

    [Fact]
    public async Task ResolveAsync_returns_null_after_revocation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await CreateDatabaseAsync();
        var service = Service(db);
        var result = await service.CreateAsync("tenant-1", "Tablet", "parent@example.com", null, cancellationToken);

        Assert.True(await service.RevokeAsync(result.Link.Id, "tenant-1", cancellationToken));
        Assert.Null(await service.ResolveAsync(result.Token, cancellationToken));
    }

    [Fact]
    public async Task ResolveAsync_returns_null_for_an_expired_link()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await CreateDatabaseAsync();
        var service = Service(db);
        var result = await service.CreateAsync("tenant-1", "Tablet", "parent@example.com", null, cancellationToken);
        result.Link.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync(cancellationToken);

        Assert.Null(await service.ResolveAsync(result.Token, cancellationToken));
    }

    [Fact]
    public async Task ResolveAsync_returns_null_when_the_tenant_is_soft_deleted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await CreateDatabaseAsync();
        var service = Service(db);
        var result = await service.CreateAsync("tenant-1", "Tablet", "parent@example.com", null, cancellationToken);
        db.Tenants.Single().Deleted = true;
        await db.SaveChangesAsync(cancellationToken);

        Assert.Null(await service.ResolveAsync(result.Token, cancellationToken));
    }

    [Fact]
    public async Task ResolveAsync_does_not_rewrite_LastAccessedAt_within_an_hour()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await CreateDatabaseAsync();
        var service = Service(db);
        var result = await service.CreateAsync("tenant-1", "Tablet", "parent@example.com", null, cancellationToken);
        await service.ResolveAsync(result.Token, cancellationToken);
        var updated = result.Link.UpdatedTimestamp;

        await service.ResolveAsync(result.Token, cancellationToken);

        Assert.Equal(updated, result.Link.UpdatedTimestamp);
    }

    [Fact]
    public async Task RevokeAsync_will_not_revoke_across_tenants()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await CreateDatabaseAsync("tenant-1", "tenant-2");
        var service = Service(db);
        var result = await service.CreateAsync("tenant-1", "Tablet", "parent@example.com", null, cancellationToken);

        Assert.False(await service.RevokeAsync(result.Link.Id, "tenant-2", cancellationToken));
        Assert.NotNull(await service.ResolveAsync(result.Token, cancellationToken));
    }

    [Fact]
    public async Task CreateAsync_rejects_an_empty_name()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await CreateDatabaseAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            Service(db).CreateAsync("tenant-1", "  ", "parent@example.com", null, cancellationToken).AsTask());
    }

    [Fact]
    public async Task Two_links_never_share_a_token()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await CreateDatabaseAsync();
        var service = Service(db);
        var results = new List<CreatedShareLink>();
        for (var i = 0; i < 50; i++)
            results.Add(await service.CreateAsync("tenant-1", $"Tablet {i}", "parent@example.com", null, cancellationToken));

        Assert.Equal(50, results.Select(x => x.Token).Distinct().Count());
        Assert.Equal(50, results.Select(x => x.Link.TokenHash).Distinct().Count());
    }

    private static ShareLinkService Service(ChildAllowanceManager.Data.AllowanceDbContext db) =>
        new(db, NullLogger<ShareLinkService>.Instance);

    private static async Task<ChildAllowanceManager.Data.AllowanceDbContext> CreateDatabaseAsync(
        params string[] tenantIds)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        foreach (var id in tenantIds.Length == 0 ? ["tenant-1"] : tenantIds)
            db.Tenants.Add(new TenantConfiguration
            {
                Id = id,
                TenantName = id,
                UrlSuffix = id
            });
        await db.SaveChangesAsync(cancellationToken);
        return db;
    }
}
