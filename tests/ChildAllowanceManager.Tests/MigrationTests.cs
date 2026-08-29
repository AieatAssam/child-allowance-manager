using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Data;
using ChildAllowanceManager.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ChildAllowanceManager.Tests;

public class MigrationTests
{
    [Fact]
    public async Task Migrating_a_fresh_database_creates_the_full_schema()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateMigratedContextAsync(cancellationToken);

        Assert.Empty(await db.Children.ToListAsync(cancellationToken));
        Assert.Empty(await db.Transactions.ToListAsync(cancellationToken));
        Assert.Empty(await db.Tenants.ToListAsync(cancellationToken));
        Assert.Empty(await db.Users.ToListAsync(cancellationToken));
        Assert.Empty(await db.DataProtectionKeys.ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task Model_has_no_pending_changes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateMigratedContextAsync(cancellationToken);

        Assert.Empty(db.Database.GetPendingMigrations());

        var migrations = db.GetService<IMigrationsAssembly>();
        var lastMigration = migrations.Migrations.Last();
        var migration = migrations.CreateMigration(lastMigration.Value, db.Database.ProviderName!);
        var differ = db.GetService<IMigrationsModelDiffer>();
        var targetModel = db.GetService<IModelRuntimeInitializer>()
            .Initialize(migration.TargetModel, designTime: true, validationLogger: null);

        Assert.False(differ.HasDifferences(
            targetModel.GetRelationalModel(),
            db.GetService<IDesignTimeModel>().Model.GetRelationalModel()));
    }

    [Fact]
    public async Task Legacy_initial_schema_can_be_migrated_after_baseline_compatibility()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        await db.Database.EnsureDeletedAsync(cancellationToken);
        await db.Database.MigrateAsync("20260810214537_Initial", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("DROP TABLE \"__EFMigrationsHistory\"", cancellationToken);
        var tenant = new TenantConfiguration
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantName = "Legacy Family",
            UrlSuffix = "legacy",
            CreatedTimestamp = DateTimeOffset.UtcNow,
            UpdatedTimestamp = DateTimeOffset.UtcNow
        };
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Tenants" ("Id", "TenantName", "UrlSuffix", "Deleted", "CreatedTimestamp", "UpdatedTimestamp")
            VALUES ({tenant.Id}, {tenant.TenantName}, {tenant.UrlSuffix}, FALSE, {tenant.CreatedTimestamp}, {tenant.UpdatedTimestamp});
            """, cancellationToken);

        await BaselineCompatibility.EnsureBaselineRecordedAsync(db, cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        Assert.Equal(tenant.Id, (await db.Tenants.SingleAsync(cancellationToken)).Id);
    }
}
