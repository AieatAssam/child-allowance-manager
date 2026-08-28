using ChildAllowanceManager.Data;
using Microsoft.EntityFrameworkCore;

namespace ChildAllowanceManager.Tests;

public static class PostgresTestDatabase
{
    public static AllowanceDbContext CreateContext()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                        ?? throw new InvalidOperationException(
                             "ConnectionStrings__Postgres must point at the Docker PostgreSQL test database.");
        var options = new DbContextOptionsBuilder<AllowanceDbContext>()
            .UseNpgsql(connection)
            .Options;
        return new AllowanceDbContext(options);
    }

    public static Task<AllowanceDbContext> CreateCleanContextAsync() =>
        CreateCleanContextAsync(TestContext.Current.CancellationToken);

    public static async Task<AllowanceDbContext> CreateCleanContextAsync(CancellationToken cancellationToken)
    {
        var db = CreateContext();
        await db.Database.EnsureDeletedAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
        return db;
    }

    public static Task<AllowanceDbContext> CreateMigratedContextAsync() =>
        CreateMigratedContextAsync(TestContext.Current.CancellationToken);

    public static async Task<AllowanceDbContext> CreateMigratedContextAsync(CancellationToken cancellationToken)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                        ?? throw new InvalidOperationException(
                             "ConnectionStrings__Postgres must point at the Docker PostgreSQL test database. Run: CAM_TEST_DB=<name> CAM_TEST_KEEP=1 bash scripts/test-postgres.sh");
        var options = new DbContextOptionsBuilder<AllowanceDbContext>()
            .UseNpgsql(connection)
            .Options;
        var db = new AllowanceDbContext(options);
        await db.Database.EnsureDeletedAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
        return db;
    }
}
