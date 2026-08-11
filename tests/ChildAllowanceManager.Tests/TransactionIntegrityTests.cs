using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Services;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChildAllowanceManager.Tests;

public class TransactionIntegrityTests
{
    [Fact]
    public async Task Failed_hold_does_not_leave_the_child_updated()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var tenant = Tenant();
        db.Tenants.Add(tenant);
        var child = new ChildConfiguration
        {
            TenantId = tenant.Id, FirstName = "Child", LastName = "One", HoldDaysRemaining = 1
        };
        db.Children.Add(child);
        await db.SaveChangesAsync();
        var notifications = new GlobalNotificationService();
        var service = new ChildService(db, notifications, new TransactionService(db, notifications),
            NullLogger<ChildService>.Instance);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ApplyHoldAsync(child.Id, tenant.Id, 2, "", "request-1").AsTask());

        Assert.Equal(1, (await service.GetChild(child.Id, tenant.Id))!.HoldDaysRemaining);
        Assert.Empty(await new TransactionService(db, notifications)
            .GetTransactionsForChild(child.Id, tenant.Id));
    }

    [Fact]
    public async Task A_hold_updates_the_child_and_records_one_hold_transaction()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var tenant = Tenant();
        db.Tenants.Add(tenant);
        var child = new ChildConfiguration { TenantId = tenant.Id, FirstName = "Child", LastName = "One" };
        db.Children.Add(child);
        await db.SaveChangesAsync();
        var transactions = new TransactionService(db, new GlobalNotificationService());
        var service = new ChildService(db, new GlobalNotificationService(), transactions,
            NullLogger<ChildService>.Instance);

        await service.ApplyHoldAsync(child.Id, tenant.Id, 2, "Holiday", "request-1");

        Assert.Equal(2, (await service.GetChild(child.Id, tenant.Id))!.HoldDaysRemaining);
        var hold = Assert.Single(await transactions.GetTransactionsForChild(child.Id, tenant.Id));
        Assert.Equal(TransactionType.Hold, hold.TransactionType);
        Assert.Equal("Holiday (2 days)", hold.Description);
    }

    [Fact]
    public async Task A_request_id_returns_the_original_transaction_without_a_second_row()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var tenant = Tenant();
        var child = new ChildConfiguration { TenantId = tenant.Id, FirstName = "Child", LastName = "One" };
        db.AddRange(tenant, child);
        await db.SaveChangesAsync();
        var service = new TransactionService(db, new GlobalNotificationService());
        var first = await service.AddTransaction(Transaction(child, 3m, "request-1"));
        var second = await service.AddTransaction(Transaction(child, 3m, "request-1"));

        Assert.Equal(first.Id, second.Id);
        Assert.Single(await service.GetTransactionsForChild(child.Id, tenant.Id));
    }

    [Fact]
    public async Task Reversal_adds_a_correction_and_preserves_the_original()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var tenant = Tenant();
        var child = new ChildConfiguration { TenantId = tenant.Id, FirstName = "Child", LastName = "One" };
        db.AddRange(tenant, child);
        await db.SaveChangesAsync();
        var service = new TransactionService(db, new GlobalNotificationService());
        var original = await service.AddTransaction(Transaction(child, 5m, null));

        var correction = await service.ReverseTransactionAsync(
            original.Id, tenant.Id, "Entered twice", "correction-1");

        Assert.Equal(-5m, correction.TransactionAmount);
        Assert.Equal(original.Id, correction.ReversesTransactionId);
        Assert.Equal(0m, await service.GetBalanceForChild(child.Id, tenant.Id));
        await Assert.ThrowsAsync<ValidationException>(() => service.ReverseTransactionAsync(
            original.Id, tenant.Id, "Again", "correction-2").AsTask());
    }

    [Fact]
    public async Task Csv_export_escapes_commas_and_quotes()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var tenant = Tenant();
        var child = new ChildConfiguration { TenantId = tenant.Id, FirstName = "Child", LastName = "One" };
        db.AddRange(tenant, child);
        await db.SaveChangesAsync();
        var service = new TransactionService(db, new GlobalNotificationService());
        await service.AddTransaction(new AllowanceTransaction
        {
            ChildId = child.Id, TenantId = child.TenantId, TransactionType = TransactionType.Deposit,
            TransactionAmount = 2m, Description = "Gift, \"special\""
        });

        var csv = await service.ExportTransactionsCsvAsync(child.Id, tenant.Id);

        Assert.Contains("\"Gift, \"\"special\"\"\"", csv);
    }

    private static TenantConfiguration Tenant() => new()
    {
        Id = "tenant-1", TenantName = "Family", UrlSuffix = "family"
    };

    private static AllowanceTransaction Transaction(
        ChildConfiguration child, decimal amount, string? requestId) => new()
    {
        ChildId = child.Id, TenantId = child.TenantId, TransactionType = TransactionType.Deposit,
        TransactionAmount = amount, Description = "Gift", RequestId = requestId
    };
}
