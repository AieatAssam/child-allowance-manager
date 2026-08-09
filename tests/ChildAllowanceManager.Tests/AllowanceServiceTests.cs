using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Data;
using ChildAllowanceManager.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChildAllowanceManager.Tests;

public class AllowanceServiceTests
{
    [Fact]
    public async Task AddingChildCreatesInitialBalanceAndTransactionsUpdateIt()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var notifications = new GlobalNotificationService();
        var transactions = new TransactionService(db, notifications);
        var children = new ChildService(db, notifications, transactions, NullLogger<ChildService>.Instance);
        var child = new ChildConfiguration
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            TenantId = "tenant-1",
            RegularAllowance = 5m
        };

        await children.AddChild(child);
        await transactions.AddTransaction(new AllowanceTransaction
        {
            ChildId = child.Id,
            TenantId = child.TenantId,
            TransactionType = TransactionType.Deposit,
            TransactionAmount = 7m,
            Description = "Bonus"
        });
        await transactions.AddTransaction(new AllowanceTransaction
        {
            ChildId = child.Id,
            TenantId = child.TenantId,
            TransactionType = TransactionType.Withdrawal,
            TransactionAmount = -3m,
            Description = "Treat"
        });

        Assert.Equal(4m, await transactions.GetBalanceForChild(child.Id, child.TenantId));
        Assert.Equal(3, (await transactions.GetTransactionsForChild(child.Id, child.TenantId)).Count());
        Assert.Equal(4m, Assert.Single(await children.GetChildrenWithBalance(child.TenantId, default)).Balance);
    }

    [Fact]
    public async Task TransactionsSupportPagingAndDailyAllowanceFiltering()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var service = new TransactionService(db, new GlobalNotificationService());
        const string childId = "child-1";
        const string tenantId = "tenant-1";
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-5);
        db.Transactions.AddRange(
            Transaction(childId, tenantId, TransactionType.DailyAllowance, 1m, timestamp),
            Transaction(childId, tenantId, TransactionType.Deposit, 2m, timestamp.AddMinutes(1)),
            Transaction(childId, tenantId, TransactionType.Withdrawal, -1m, timestamp.AddMinutes(2)));
        await db.SaveChangesAsync();

        var page = await service.GetPagedTransactionsForChild(childId, tenantId, 0, 200);
        var withoutDaily = await service.GetPagedTransactionsForChild(childId, tenantId, 1, 10, true);

        Assert.Equal(3, page.Total);
        Assert.Equal(3, page.Items.Count);
        Assert.Equal(2, withoutDaily.Total);
        Assert.DoesNotContain(withoutDaily.Items, x => x.TransactionType == TransactionType.DailyAllowance);
        Assert.Equal(1, page.Page);
        Assert.Equal(100, page.PageSize);
    }

    [Fact]
    public async Task BalanceHistoryFillsMissingDatesWithTheLastKnownBalance()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var service = new TransactionService(db, new GlobalNotificationService());
        const string childId = "child-1";
        const string tenantId = "tenant-1";
        var first = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        db.Transactions.AddRange(
            Transaction(childId, tenantId, TransactionType.Deposit, 1m, first, 1m),
            Transaction(childId, tenantId, TransactionType.Deposit, 2m, first.AddDays(2), 3m));
        await db.SaveChangesAsync();

        var history = (await service.GetBalanceHistoryForChild(childId, tenantId, null, null, default)).ToArray();

        Assert.Equal(3, history.Length);
        Assert.Equal(first.Date, history[0].Timestamp.Date);
        Assert.Equal(1m, history[1].Balance);
        Assert.Equal(first.AddDays(2).Date, history[2].Timestamp.Date);
        Assert.Equal(3m, history[2].Balance);
    }

    [Fact]
    public async Task DeletingChildHidesItWithoutDeletingItsTransactions()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var notifications = new GlobalNotificationService();
        var transactions = new TransactionService(db, notifications);
        var children = new ChildService(db, notifications, transactions, NullLogger<ChildService>.Instance);
        var child = new ChildConfiguration { FirstName = "Ada", LastName = "Lovelace", TenantId = "tenant-1" };
        await children.AddChild(child);

        Assert.True(await children.DeleteChild(child.Id, child.TenantId));
        Assert.Empty(await children.GetChildren(child.TenantId));
        Assert.NotNull(await transactions.GetLatestTransactionForChild(child.Id, child.TenantId));
    }

    [Fact]
    public async Task TransactionQueriesStayScopedToTheRequestedChildAndTenant()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var service = new TransactionService(db, new GlobalNotificationService());
        var timestamp = DateTimeOffset.UtcNow;
        db.Transactions.AddRange(
            Transaction("child-1", "tenant-1", TransactionType.Deposit, 1m, timestamp),
            Transaction("child-2", "tenant-1", TransactionType.Deposit, 2m, timestamp.AddMinutes(1)),
            Transaction("child-1", "tenant-2", TransactionType.Deposit, 3m, timestamp.AddMinutes(2)));
        await db.SaveChangesAsync();

        var result = await service.GetTransactionsForChild("child-1", "tenant-1");

        Assert.Single(result);
        Assert.Equal(1m, result.Single().TransactionAmount);
    }

    [Fact]
    public async Task BalanceHistoryHonorsDateBoundsAndStillFillsGaps()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var service = new TransactionService(db, new GlobalNotificationService());
        const string childId = "child-1";
        const string tenantId = "tenant-1";
        var first = new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero);
        db.Transactions.AddRange(
            Transaction(childId, tenantId, TransactionType.Deposit, 1m, first, 1m),
            Transaction(childId, tenantId, TransactionType.Deposit, 1m, first.AddDays(1), 2m),
            Transaction(childId, tenantId, TransactionType.Deposit, 2m, first.AddDays(3), 4m));
        await db.SaveChangesAsync();

        var history = (await service.GetBalanceHistoryForChild(
            childId, tenantId, first.AddDays(1), first.AddDays(3).AddHours(1), default)).ToArray();

        Assert.Equal(3, history.Length);
        Assert.Equal(2m, history[0].Balance);
        Assert.Equal(first.AddDays(2).Date, history[1].Timestamp.Date);
        Assert.Equal(4m, history[2].Balance);
    }

    [Fact]
    public async Task ChildBalanceUsesBirthdayAllowanceOnTheChildsBirthday()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var service = new ChildService(
            db,
            new GlobalNotificationService(),
            new TransactionService(db, new GlobalNotificationService()),
            NullLogger<ChildService>.Instance);
        var child = new ChildConfiguration
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            TenantId = "tenant-1",
            BirthDate = DateTime.Today,
            RegularAllowance = 5m,
            BirthdayAllowance = 25m
        };
        var timestamp = DateTimeOffset.UtcNow.AddDays(-1);
        db.Children.Add(child);
        db.Transactions.Add(Transaction(child.Id, child.TenantId, TransactionType.Adjustment, 2m, timestamp, 2m));
        await db.SaveChangesAsync();

        var result = Assert.Single(await service.GetChildrenWithBalance(child.TenantId, default));

        Assert.True(result.IsBirthday);
        Assert.Equal(25m, result.NextRegularChange);
        Assert.Equal(2m, result.Balance);
    }

    [Fact]
    public async Task UpdatingChildPersistsChangesAndRaisesNotification()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var notifications = new GlobalNotificationService();
        IGlobalNotificationService.ChildStateChangedEventArgs? notification = null;
        notifications.ChildStateChanged += (_, args) => notification = args;
        var service = new ChildService(
            db, notifications, new TransactionService(db, notifications), NullLogger<ChildService>.Instance);
        var child = new ChildConfiguration { FirstName = "Ada", LastName = "Lovelace", TenantId = "tenant-1" };
        db.Children.Add(child);
        await db.SaveChangesAsync();

        var editable = await service.GetChild(child.Id, child.TenantId);
        editable!.FirstName = "Augusta";
        await service.UpdateChild(editable);

        Assert.Equal("Augusta", (await service.GetChild(child.Id, child.TenantId))!.FirstName);
        Assert.NotNull(notification);
        Assert.Equal(child.Id, notification.ChildId);
        Assert.Equal(child.TenantId, notification.TenantId);
        Assert.Empty(notification.NotificationMessage);
    }

    private static AllowanceTransaction Transaction(
        string childId, string tenantId, TransactionType type, decimal amount,
        DateTimeOffset timestamp, decimal? balance = null) => new()
    {
        ChildId = childId,
        TenantId = tenantId,
        TransactionType = type,
        TransactionAmount = amount,
        Balance = balance ?? amount,
        TransactionTimestamp = timestamp,
        CreatedTimestamp = timestamp,
        UpdatedTimestamp = timestamp,
        Description = type.ToString()
    };
}
