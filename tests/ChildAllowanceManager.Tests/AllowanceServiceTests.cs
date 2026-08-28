using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Data;
using ChildAllowanceManager.Services;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChildAllowanceManager.Tests;

public class AllowanceServiceTests
{
    [Fact]
    public async Task AddingChildCreatesInitialBalanceAndTransactionsUpdateIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        await using var transactionDb = PostgresTestDatabase.CreateContext();
        db.Tenants.Add(Tenant("tenant-1"));
        await db.SaveChangesAsync(cancellationToken);
        var notifications = new GlobalNotificationService();
        var transactions = new TransactionService(transactionDb, notifications);
        var children = new ChildService(db, notifications, transactions, NullLogger<ChildService>.Instance);
        var child = new ChildConfiguration
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            TenantId = "tenant-1",
            RegularAllowance = 5m
        };

        await children.AddChild(child, cancellationToken);
        await transactions.AddTransaction(new AllowanceTransaction
        {
            ChildId = child.Id,
            TenantId = child.TenantId,
            TransactionType = TransactionType.Deposit,
            TransactionAmount = 7m,
            Description = "Bonus"
        }, cancellationToken);
        await transactions.AddTransaction(new AllowanceTransaction
        {
            ChildId = child.Id,
            TenantId = child.TenantId,
            TransactionType = TransactionType.Withdrawal,
            TransactionAmount = -3m,
            Description = "Treat"
        }, cancellationToken);

        Assert.Equal(4m, await transactions.GetBalanceForChild(child.Id, child.TenantId, cancellationToken));
        Assert.Equal(3, (await transactions.GetTransactionsForChild(child.Id, child.TenantId, cancellationToken: cancellationToken)).Count());
        Assert.Equal(4m, Assert.Single(await children.GetChildrenWithBalance(child.TenantId, cancellationToken)).Balance);
    }

    [Fact]
    public async Task TransactionsSupportPagingAndDailyAllowanceFiltering()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        var service = new TransactionService(db, new GlobalNotificationService());
        const string childId = "child-1";
        const string tenantId = "tenant-1";
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-5);
        db.AddRange(Tenant(tenantId), Child(childId, tenantId));
        await db.SaveChangesAsync(cancellationToken);
        db.Transactions.AddRange(
            Transaction(childId, tenantId, TransactionType.DailyAllowance, 1m, timestamp),
            Transaction(childId, tenantId, TransactionType.Deposit, 2m, timestamp.AddMinutes(1)),
            Transaction(childId, tenantId, TransactionType.Withdrawal, -1m, timestamp.AddMinutes(2)));
        await db.SaveChangesAsync(cancellationToken);

        var page = await service.GetPagedTransactionsForChild(childId, tenantId, 0, 200, cancellationToken: cancellationToken);
        var withoutDaily = await service.GetPagedTransactionsForChild(childId, tenantId, 1, 10, true, cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        var service = new TransactionService(db, new GlobalNotificationService());
        const string childId = "child-1";
        const string tenantId = "tenant-1";
        var first = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        db.AddRange(Tenant(tenantId), Child(childId, tenantId));
        await db.SaveChangesAsync(cancellationToken);
        db.Transactions.AddRange(
            Transaction(childId, tenantId, TransactionType.Deposit, 1m, first, 1m),
            Transaction(childId, tenantId, TransactionType.Deposit, 2m, first.AddDays(2), 3m));
        await db.SaveChangesAsync(cancellationToken);

        var history = (await service.GetBalanceHistoryForChild(childId, tenantId, null, null, cancellationToken)).ToArray();

        Assert.Equal(3, history.Length);
        Assert.Equal(first.Date, history[0].Timestamp.Date);
        Assert.Equal(1m, history[1].Balance);
        Assert.Equal(first.AddDays(2).Date, history[2].Timestamp.Date);
        Assert.Equal(3m, history[2].Balance);
    }

    [Fact]
    public async Task ChildBalanceHistoryBatchPathFillsMissingDates()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        var notifications = new GlobalNotificationService();
        var transactions = new TransactionService(db, notifications);
        var service = new ChildService(db, notifications, transactions, NullLogger<ChildService>.Instance);
        var child = new ChildConfiguration
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            TenantId = "tenant-1"
        };
        db.Tenants.Add(Tenant(child.TenantId));
        var first = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        db.Children.Add(child);
        db.Transactions.AddRange(
            Transaction(child.Id, child.TenantId, TransactionType.Deposit, 1m, first, 1m),
            Transaction(child.Id, child.TenantId, TransactionType.Deposit, 2m, first.AddDays(2), 3m));
        await db.SaveChangesAsync(cancellationToken);

        var history = Assert.Single(await service.GetChildrenWithBalanceHistory(child.TenantId, null, null, cancellationToken));

        Assert.Equal(3, history.BalanceHistory.Length);
        Assert.Equal(1m, history.BalanceHistory[1].Balance);
    }

    [Fact]
    public async Task DeletingChildHidesItWithoutDeletingItsTransactions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        var notifications = new GlobalNotificationService();
        var transactions = new TransactionService(db, notifications);
        var children = new ChildService(db, notifications, transactions, NullLogger<ChildService>.Instance);
        db.Tenants.Add(Tenant("tenant-1"));
        var child = new ChildConfiguration { FirstName = "Ada", LastName = "Lovelace", TenantId = "tenant-1" };
        await children.AddChild(child, cancellationToken);

        Assert.True(await children.DeleteChild(child.Id, child.TenantId, cancellationToken));
        Assert.Empty(await children.GetChildren(child.TenantId, cancellationToken));
        Assert.NotNull(await transactions.GetLatestTransactionForChild(child.Id, child.TenantId, cancellationToken));
    }

    [Fact]
    public async Task TransactionQueriesStayScopedToTheRequestedChildAndTenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        var service = new TransactionService(db, new GlobalNotificationService());
        var timestamp = DateTimeOffset.UtcNow;
        db.AddRange(
            Tenant("tenant-1"), Tenant("tenant-2"),
            Child("child-1", "tenant-1"), Child("child-2", "tenant-1"));
        await db.SaveChangesAsync(cancellationToken);
        db.Transactions.AddRange(
            Transaction("child-1", "tenant-1", TransactionType.Deposit, 1m, timestamp),
            Transaction("child-2", "tenant-1", TransactionType.Deposit, 2m, timestamp.AddMinutes(1)),
            Transaction("child-1", "tenant-2", TransactionType.Deposit, 3m, timestamp.AddMinutes(2)));
        await db.SaveChangesAsync(cancellationToken);

        var result = await service.GetTransactionsForChild("child-1", "tenant-1", cancellationToken: cancellationToken);

        Assert.Single(result);
        Assert.Equal(1m, result.Single().TransactionAmount);
    }

    [Fact]
    public async Task BalanceHistoryHonorsDateBoundsAndStillFillsGaps()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        var service = new TransactionService(db, new GlobalNotificationService());
        const string childId = "child-1";
        const string tenantId = "tenant-1";
        var first = new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero);
        db.AddRange(Tenant(tenantId), Child(childId, tenantId));
        await db.SaveChangesAsync(cancellationToken);
        db.Transactions.AddRange(
            Transaction(childId, tenantId, TransactionType.Deposit, 1m, first, 1m),
            Transaction(childId, tenantId, TransactionType.Deposit, 1m, first.AddDays(1), 2m),
            Transaction(childId, tenantId, TransactionType.Deposit, 2m, first.AddDays(3), 4m));
        await db.SaveChangesAsync(cancellationToken);

        var history = (await service.GetBalanceHistoryForChild(
            childId, tenantId, first.AddDays(1), first.AddDays(3).AddHours(1), cancellationToken)).ToArray();

        Assert.Equal(3, history.Length);
        Assert.Equal(2m, history[0].Balance);
        Assert.Equal(first.AddDays(2).Date, history[1].Timestamp.Date);
        Assert.Equal(4m, history[2].Balance);
    }

    [Fact]
    public async Task ChildBalanceUsesBirthdayAllowanceOnTheChildsBirthday()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        var tenantToday = DateTime.UtcNow.Date;
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
            BirthDate = tenantToday,
            RegularAllowance = 5m,
            BirthdayAllowance = 25m
        };
        var tenant = Tenant(child.TenantId);
        tenant.TimeZoneId = "UTC";
        db.Tenants.Add(tenant);
        var timestamp = DateTimeOffset.UtcNow.AddDays(-1);
        db.Children.Add(child);
        db.Transactions.Add(Transaction(child.Id, child.TenantId, TransactionType.Adjustment, 2m, timestamp, 2m));
        await db.SaveChangesAsync(cancellationToken);

        var result = Assert.Single(await service.GetChildrenWithBalance(child.TenantId, cancellationToken));

        Assert.True(result.IsBirthday);
        Assert.Equal(25m, result.NextRegularChange);
        Assert.Equal(2m, result.Balance);
    }

    [Fact]
    public async Task NextAllowanceDateIsAlwaysInTheFuture()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
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
            RegularAllowance = 5m
        };
        db.Tenants.Add(Tenant(child.TenantId));
        db.Children.Add(child);
        var allowanceTimestamp = DateTimeOffset.UtcNow.AddDays(-1);
        var allowance = Transaction(
            child.Id, child.TenantId, TransactionType.DailyAllowance, 5m,
            allowanceTimestamp, 5m);
        allowance.AllowanceDate = allowanceTimestamp.Date;
        db.Transactions.Add(allowance);
        await db.SaveChangesAsync(cancellationToken);

        var before = DateTimeOffset.UtcNow;
        var result = Assert.Single(await service.GetChildrenWithBalance(child.TenantId, cancellationToken));

        Assert.True(result.NextRegularChangeDate > before);
    }

    [Fact]
    public async Task UpdatingChildPersistsChangesAndRaisesNotification()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        var notifications = new GlobalNotificationService();
        IGlobalNotificationService.ChildStateChangedEventArgs? notification = null;
        notifications.ChildStateChanged += (_, args) => notification = args;
        var service = new ChildService(
            db, notifications, new TransactionService(db, notifications), NullLogger<ChildService>.Instance);
        db.Tenants.Add(Tenant("tenant-1"));
        var child = new ChildConfiguration { FirstName = "Ada", LastName = "Lovelace", TenantId = "tenant-1" };
        db.Children.Add(child);
        await db.SaveChangesAsync(cancellationToken);

        var editable = await service.GetChild(child.Id, child.TenantId, cancellationToken);
        editable!.FirstName = "Augusta";
        await service.UpdateChild(editable, cancellationToken);

        Assert.Equal("Augusta", (await service.GetChild(child.Id, child.TenantId, cancellationToken))!.FirstName);
        Assert.NotNull(notification);
        Assert.Equal(child.Id, notification.ChildId);
        Assert.Equal(child.TenantId, notification.TenantId);
        Assert.Empty(notification.NotificationMessage);
    }

    [Fact]
    public async Task ChildUpdatesCannotMoveAChildToAnotherTenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        var notifications = new GlobalNotificationService();
        var transactions = new TransactionService(db, notifications);
        var service = new ChildService(db, notifications, transactions, NullLogger<ChildService>.Instance);
        db.Tenants.Add(Tenant("tenant-1"));
        var child = await service.AddChild(new ChildConfiguration
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            TenantId = "tenant-1"
        }, cancellationToken);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateChild(new ChildConfiguration
        {
            Id = child.Id,
            FirstName = "Ada",
            LastName = "Lovelace",
            TenantId = "tenant-2"
        }, cancellationToken).AsTask());
    }

    [Fact]
    public async Task TransactionsRejectInvalidAmountsAndUnknownChildren()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        var service = new TransactionService(db, new GlobalNotificationService());
        var child = new ChildConfiguration
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            TenantId = "tenant-1"
        };
        db.Tenants.Add(Tenant(child.TenantId));
        db.Children.Add(child);
        await db.SaveChangesAsync(cancellationToken);

        await Assert.ThrowsAsync<ValidationException>(() => service.AddTransaction(new AllowanceTransaction
        {
            ChildId = child.Id,
            TenantId = child.TenantId,
            TransactionType = TransactionType.Deposit,
            TransactionAmount = -1m,
            Description = "Invalid"
        }, cancellationToken).AsTask());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AddTransaction(new AllowanceTransaction
        {
            ChildId = "missing",
            TenantId = child.TenantId,
            TransactionType = TransactionType.Deposit,
            TransactionAmount = 1m,
            Description = "Unknown child"
        }, cancellationToken).AsTask());
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

    private static TenantConfiguration Tenant(string id) => new()
    {
        Id = id,
        TenantName = id,
        UrlSuffix = id
    };

    private static ChildConfiguration Child(string id, string tenantId) => new()
    {
        Id = id,
        FirstName = id,
        LastName = "Test",
        TenantId = tenantId
    };
}
