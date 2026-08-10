using ChildAllowanceManager.Data;
using Microsoft.EntityFrameworkCore;

namespace ChildAllowanceManager.Tests;

public static class PostgresTestDatabase
{
    public static async Task<AllowanceDbContext> CreateCleanContextAsync()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                         ?? throw new InvalidOperationException(
                             "ConnectionStrings__Postgres must point at the Docker PostgreSQL test database.");
        var options = new DbContextOptionsBuilder<AllowanceDbContext>()
            .UseNpgsql(connection)
            .Options;
        var db = new AllowanceDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    public static async Task<AllowanceDbContext> CreateMigratedContextAsync()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                         ?? throw new InvalidOperationException(
                             "ConnectionStrings__Postgres must point at the Docker PostgreSQL test database.");
        var options = new DbContextOptionsBuilder<AllowanceDbContext>()
            .UseNpgsql(connection)
            .Options;
        var db = new AllowanceDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        return db;
    }
}
