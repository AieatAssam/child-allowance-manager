using ChildAllowanceManager.Data;
using Microsoft.EntityFrameworkCore;

namespace ChildAllowanceManager.Migrations;

public static class BaselineCompatibility
{
    public static async Task EnsureBaselineRecordedAsync(AllowanceDbContext db, CancellationToken ct)
    {
        var historyExists = await db.Database
            .SqlQueryRaw<bool>("SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL AS \"Value\"")
            .SingleAsync(ct);
        if (historyExists)
            return;

        var tenantsExist = await db.Database
            .SqlQueryRaw<bool>("SELECT to_regclass('public.\"Tenants\"') IS NOT NULL AS \"Value\"")
            .SingleAsync(ct);
        if (!tenantsExist)
            return;

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId"));
            """, ct);

        var productVersion = db.Model.GetProductVersion()
            ?? throw new InvalidOperationException("EF product version is unavailable.");
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({0}, {1});",
            [db.Database.GetMigrations().First(), productVersion], ct);
    }
}
