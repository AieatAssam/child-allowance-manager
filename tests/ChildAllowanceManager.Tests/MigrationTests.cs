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
        await using var db = await PostgresTestDatabase.CreateMigratedContextAsync();

        Assert.Empty(await db.Children.ToListAsync());
        Assert.Empty(await db.Transactions.ToListAsync());
        Assert.Empty(await db.Tenants.ToListAsync());
        Assert.Empty(await db.Users.ToListAsync());
    }

    [Fact]
    public async Task Model_has_no_pending_changes()
    {
        await using var db = await PostgresTestDatabase.CreateMigratedContextAsync();

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
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync("20260810214537_Initial");
        await db.Database.ExecuteSqlRawAsync("DROP TABLE \"__EFMigrationsHistory\"");
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
            """);

        await BaselineCompatibility.EnsureBaselineRecordedAsync(db, CancellationToken.None);
        await db.Database.MigrateAsync();

        Assert.Equal(tenant.Id, (await db.Tenants.SingleAsync()).Id);
    }
}
