using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Data;
using ChildAllowanceManager.Services;
using ChildAllowanceManager.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;

namespace ChildAllowanceManager.Tests;

public class PostgresEndToEndTests
{
    [Fact]
    public async Task MigrationsCreateTheFreshDatabaseSchema()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateMigratedContextAsync(cancellationToken);

        Assert.Empty(await db.Database.GetPendingMigrationsAsync(cancellationToken));
        Assert.True(await db.Database.CanConnectAsync(cancellationToken));
    }

    [Fact]
    public async Task DevelopmentSeederCreatesReusableDemoWorkspace()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        var seeder = new DevelopmentDataSeeder(db);

        await seeder.SeedAsync(cancellationToken);
        await seeder.SeedAsync(cancellationToken);

        var tenant = await db.Tenants.SingleAsync(x => x.Id == DevelopmentDataSeeder.TenantId, cancellationToken);
        var user = await db.Users.SingleAsync(x => x.Email == DevelopmentDataSeeder.UserEmail, cancellationToken);

        Assert.Equal("demo", tenant.UrlSuffix);
        Assert.Equal([DevelopmentDataSeeder.TenantId], user.Tenants);
        Assert.Contains(ValidRoles.Admin, user.Roles);
        Assert.Contains(ValidRoles.Parent, user.Roles);
        Assert.Equal(2, await db.Children.CountAsync(x => x.TenantId == DevelopmentDataSeeder.TenantId, cancellationToken));
        Assert.Equal(21, await db.Transactions.CountAsync(x => x.TenantId == DevelopmentDataSeeder.TenantId, cancellationToken));
        Assert.Equal(40m, (await db.Transactions.SingleAsync(x => x.Id == "development-transaction-1", cancellationToken)).Balance);
        Assert.Equal(3m, (await db.Transactions.SingleAsync(x => x.Id == "development-transaction-3", cancellationToken)).Balance);
    }

    [Fact]
    public async Task SeededDemoTenantSupportsParentActionsAndManualDailyWorkerRun()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        var notifications = new GlobalNotificationService();
        var (tenants, _, children, transactions) = Services(db, notifications);
        await new DevelopmentDataSeeder(db).SeedAsync(cancellationToken);

        var demo = await tenants.GetTenantBySuffix(DevelopmentDataSeeder.TenantSuffix, cancellationToken);
        Assert.NotNull(demo);
        var seededChildren = (await children.GetChildrenWithBalance(demo!.Id, cancellationToken)).ToArray();
        var alex = seededChildren.Single(x => x.Id == "development-child-1");
        var sam = seededChildren.Single(x => x.Id == "development-child-2");
        Assert.Equal(40m, alex.Balance);
        Assert.Equal(3m, sam.Balance);

        await transactions.AddTransaction(new AllowanceTransaction
        {
            ChildId = alex.Id,
            TenantId = demo.Id,
            TransactionType = TransactionType.Deposit,
            TransactionAmount = 7m,
            Description = "Saved gift"
        }, cancellationToken);
        await transactions.AddTransaction(new AllowanceTransaction
        {
            ChildId = sam.Id,
            TenantId = demo.Id,
            TransactionType = TransactionType.Withdrawal,
            TransactionAmount = -1m,
            Description = "Small treat"
        }, cancellationToken);
        Assert.Equal(47m, await transactions.GetBalanceForChild(alex.Id, demo.Id, cancellationToken));
        Assert.Equal(2m, await transactions.GetBalanceForChild(sam.Id, demo.Id, cancellationToken));
        Assert.Equal(6, (await transactions.GetPagedTransactionsForChild(
            sam.Id, demo.Id, 1, 25, ignoreDailyAllowance: true, cancellationToken: cancellationToken)).Total);

        var alexConfiguration = (await children.GetChild(alex.Id, demo.Id, cancellationToken))!;
        alexConfiguration.HoldDaysRemaining = 1;
        await children.UpdateChild(alexConfiguration, cancellationToken);

        var job = new DailyAllowanceJob(
            transactions, children, tenants,
            NullLogger<DailyAllowanceJob>.Instance);
        await job.Execute(new TestJobExecutionContext(LocalMidnightUtc(demo.TimeZoneId)));

        Assert.Equal(47m, await transactions.GetBalanceForChild(alex.Id, demo.Id, cancellationToken));
        Assert.Equal(5m, await transactions.GetBalanceForChild(sam.Id, demo.Id, cancellationToken));
        Assert.Equal(0, (await children.GetChild(alex.Id, demo.Id, cancellationToken))!.HoldDaysRemaining);
    }

    [Fact]
    public async Task FullLifecycleKeepsServicesAndStoredDataConsistent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        var (tenants, users, children, transactions) = Services(db);

        var tenant = await tenants.AddTenant(new TenantConfiguration
        {
            TenantName = "The Lovelace Family",
            UrlSuffix = "lovelace"
        }, cancellationToken);
        Assert.Equal(tenant.Id, (await tenants.GetTenantBySuffix("LOVELACE", cancellationToken))!.Id);
        tenant.TenantName = "Lovelace Home";
        await tenants.UpdateTenant(tenant, cancellationToken);
        Assert.Equal("Lovelace Home", (await tenants.GetTenant(tenant.Id, cancellationToken))!.TenantName);
        Assert.Single(await tenants.GetTenants(cancellationToken));

        var admin = await users.InitializeUserAsync(
            " Admin@Example.com ", "Ada", tenant.Id, cancellationToken);
        await users.AddUserToTenantAsync(
            "parent@example.com", "Charles", tenant.Id, ValidRoles.Parent, cancellationToken);
        var parent = await users.GetUserByEmailAsync("PARENT@EXAMPLE.COM", cancellationToken);
        parent!.Name = "Charles Babbage";
        await users.UpsertUserAsync(parent, cancellationToken);
        Assert.Contains(ValidRoles.Admin, admin.Roles);
        Assert.Equal(2, (await users.GetTenantUsersInRole(tenant.Id, ValidRoles.Parent, cancellationToken)).Count());
        Assert.Equal(2, (await users.GetUsersAsync(cancellationToken)).Count());

        var child = await children.AddChild(new ChildConfiguration
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            TenantId = tenant.Id,
            RegularAllowance = 5m
        }, cancellationToken);
        child.FirstName = "Augusta";
        child.HoldDaysRemaining = 1;
        await children.UpdateChild(child, cancellationToken);
        Assert.Equal("Augusta", (await children.GetChild(child.Id, tenant.Id, cancellationToken))!.FirstName);
        Assert.Single(await children.GetChildren(tenant.Id, cancellationToken));

        await transactions.AddTransaction(new AllowanceTransaction
        {
            ChildId = child.Id,
            TenantId = tenant.Id,
            TransactionType = TransactionType.Hold,
            Description = "School holiday (1 day)"
        }, cancellationToken);
        await transactions.AddTransaction(new AllowanceTransaction
        {
            ChildId = child.Id,
            TenantId = tenant.Id,
            TransactionType = TransactionType.DailyAllowance,
            TransactionAmount = 5m,
            Description = "Daily allowance"
        }, cancellationToken);
        await transactions.AddTransaction(new AllowanceTransaction
        {
            ChildId = child.Id,
            TenantId = tenant.Id,
            TransactionType = TransactionType.Deposit,
            TransactionAmount = 10m,
            Description = "Birthday gift"
        }, cancellationToken);
        await transactions.AddTransaction(new AllowanceTransaction
        {
            ChildId = child.Id,
            TenantId = tenant.Id,
            TransactionType = TransactionType.Withdrawal,
            TransactionAmount = -3m,
            Description = "Book"
        }, cancellationToken);

        Assert.Equal(12m, await transactions.GetBalanceForChild(child.Id, tenant.Id, cancellationToken));
        Assert.Equal(5, (await transactions.GetTransactionsForChild(child.Id, tenant.Id, cancellationToken: cancellationToken)).Count());
        var page = await transactions.GetPagedTransactionsForChild(child.Id, tenant.Id, 0, 200, cancellationToken: cancellationToken);
        var withoutDaily = await transactions.GetPagedTransactionsForChild(
            child.Id, tenant.Id, 1, 100, true, cancellationToken: cancellationToken);
        Assert.Equal(5, page.Total);
        Assert.Equal(100, page.PageSize);
        Assert.Equal(4, withoutDaily.Total);
        Assert.Equal(TransactionType.DailyAllowance,
            (await transactions.GetLatestRegularTransactionForChild(child.Id, tenant.Id, cancellationToken))!.TransactionType);
        Assert.Equal(TransactionType.Withdrawal,
            (await transactions.GetLatestTransactionForChild(child.Id, tenant.Id, cancellationToken))!.TransactionType);
        Assert.Single(await children.GetChildrenWithBalance(tenant.Id, cancellationToken));
        Assert.Single(await children.GetChildrenWithBalanceHistory(tenant.Id, null, null, cancellationToken));

        await users.DeleteUserAsync("parent@example.com", cancellationToken);
        Assert.Null(await users.GetUserByEmailAsync("parent@example.com", cancellationToken));
        Assert.True(await children.DeleteChild(child.Id, tenant.Id, cancellationToken));
        Assert.Null(await children.GetChild(child.Id, tenant.Id, cancellationToken));
        Assert.Equal(5, await db.Transactions.CountAsync(x => x.ChildId == child.Id, cancellationToken));

        var retainedChild = await children.AddChild(new ChildConfiguration
        {
            FirstName = "Byron",
            LastName = "Lovelace",
            TenantId = tenant.Id
        }, cancellationToken);
        Assert.True(await tenants.DeleteTenant(tenant.Id, cancellationToken));
        Assert.Null(await tenants.GetTenant(tenant.Id, cancellationToken));
        Assert.Empty(await children.GetChildren(tenant.Id, cancellationToken));
        Assert.True((await db.Children.FindAsync([retainedChild.Id], cancellationToken))!.Deleted);
        Assert.Empty((await users.GetUserByEmailAsync("admin@example.com", cancellationToken))!.Tenants);
    }

    [Fact]
    public async Task DailyWorkerAppliesDueAndBirthdayAllowancesAndConsumesHolds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        var notifications = new GlobalNotificationService();
        var (tenants, _, children, transactions) = Services(db, notifications);
        var messages = new List<string>();
        notifications.ChildStateChanged += (_, args) => messages.Add(args.NotificationMessage);
        var tenant = await tenants.AddTenant(new TenantConfiguration
        {
            TenantName = "Worker Family",
            UrlSuffix = "worker",
            TimeZoneId = "UTC"
        }, cancellationToken);
        var tenantToday = DateTime.UtcNow.Date;

        var due = await children.AddChild(new ChildConfiguration
        {
            FirstName = "Due",
            LastName = "Child",
            TenantId = tenant.Id,
            RegularAllowance = 5m
        }, cancellationToken);
        var birthday = await children.AddChild(new ChildConfiguration
        {
            FirstName = "Birthday",
            LastName = "Child",
            TenantId = tenant.Id,
            BirthDate = tenantToday,
            RegularAllowance = 5m,
            BirthdayAllowance = 20m
        }, cancellationToken);
        var held = await children.AddChild(new ChildConfiguration
        {
            FirstName = "Held",
            LastName = "Child",
            TenantId = tenant.Id,
            RegularAllowance = 3m,
            HoldDaysRemaining = 1
        }, cancellationToken);

        var job = new DailyAllowanceJob(
            transactions, children, tenants,
            NullLogger<DailyAllowanceJob>.Instance);
        await job.Execute(new TestJobExecutionContext(LocalMidnightUtc(tenant.TimeZoneId)));

        Assert.Equal(TransactionType.DailyAllowance,
            (await transactions.GetLatestTransactionForChild(due.Id, tenant.Id, cancellationToken))!.TransactionType);
        var birthdayTransaction = await transactions.GetLatestTransactionForChild(birthday.Id, tenant.Id, cancellationToken);
        Assert.Equal(TransactionType.BirthdayAllowance, birthdayTransaction!.TransactionType);
        Assert.Equal(20m, birthdayTransaction.TransactionAmount);
        Assert.Equal(5m, await transactions.GetBalanceForChild(due.Id, tenant.Id, cancellationToken));
        Assert.Equal(20m, await transactions.GetBalanceForChild(birthday.Id, tenant.Id, cancellationToken));
        var heldTransactions = await transactions.GetTransactionsForChild(held.Id, tenant.Id, cancellationToken: cancellationToken);
        Assert.Contains(heldTransactions, x => x.TransactionType == TransactionType.Hold);
        Assert.DoesNotContain(heldTransactions,
            x => x.TransactionType is TransactionType.DailyAllowance or TransactionType.BirthdayAllowance);
        Assert.Equal(0, (await children.GetChild(held.Id, tenant.Id, cancellationToken))!.HoldDaysRemaining);
        Assert.Contains(messages, message => message.Contains("daily allowance"));
        Assert.Contains(messages, message => message.Contains("birthday allowance"));
    }

    [Fact]
    public async Task TenantAndChildQueriesCannotCrossTenantBoundariesAndBalancesReconcile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        var notifications = new GlobalNotificationService();
        var (tenants, _, children, transactions) = Services(db, notifications);
        var firstTenant = await tenants.AddTenant(new TenantConfiguration
        {
            TenantName = "First Family",
            UrlSuffix = "first"
        }, cancellationToken);
        var secondTenant = await tenants.AddTenant(new TenantConfiguration
        {
            TenantName = "Second Family",
            UrlSuffix = "second"
        }, cancellationToken);
        var firstChild = await children.AddChild(new ChildConfiguration
        {
            FirstName = "First",
            LastName = "Child",
            TenantId = firstTenant.Id
        }, cancellationToken);
        var secondChild = await children.AddChild(new ChildConfiguration
        {
            FirstName = "Second",
            LastName = "Child",
            TenantId = secondTenant.Id
        }, cancellationToken);
        await transactions.AddTransaction(new AllowanceTransaction
        {
            ChildId = firstChild.Id,
            TenantId = firstTenant.Id,
            TransactionType = TransactionType.Deposit,
            TransactionAmount = 7m,
            Description = "First deposit"
        }, cancellationToken);
        await transactions.AddTransaction(new AllowanceTransaction
        {
            ChildId = secondChild.Id,
            TenantId = secondTenant.Id,
            TransactionType = TransactionType.Deposit,
            TransactionAmount = 11m,
            Description = "Second deposit"
        }, cancellationToken);

        Assert.Null(await children.GetChild(firstChild.Id, secondTenant.Id, cancellationToken));
        Assert.DoesNotContain(firstChild.Id, (await children.GetChildren(secondTenant.Id, cancellationToken)).Select(child => child.Id));
        Assert.Empty(await transactions.GetTransactionsForChild(firstChild.Id, secondTenant.Id, cancellationToken: cancellationToken));
        Assert.Equal(0m, await transactions.GetBalanceForChild(firstChild.Id, secondTenant.Id, cancellationToken));

        foreach (var child in new[] { firstChild, secondChild })
        {
            var ordered = (await transactions.GetTransactionsForChild(child.Id, child.TenantId, cancellationToken: cancellationToken))
                .OrderBy(x => x.TransactionTimestamp)
                .ToArray();
            var balance = 0m;
            foreach (var transaction in ordered)
            {
                balance += transaction.TransactionAmount;
                Assert.Equal(balance, transaction.Balance);
            }
            Assert.Equal(balance, await transactions.GetBalanceForChild(child.Id, child.TenantId, cancellationToken));
        }

        var duplicate = new TenantConfiguration
        {
            TenantName = "Duplicate",
            UrlSuffix = "first"
        };
        db.Tenants.Add(duplicate);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(cancellationToken));
    }

    [Fact]
    public async Task Share_link_lifecycle_end_to_end()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateMigratedContextAsync(cancellationToken);
        var (tenants, _, children, transactions) = Services(db);
        var tenant = await tenants.AddTenant(new TenantConfiguration
        {
            TenantName = "Share Family",
            UrlSuffix = "share-family"
        }, cancellationToken);
        var first = await children.AddChild(new ChildConfiguration
        {
            FirstName = "Ada", LastName = "Lovelace", TenantId = tenant.Id
        }, cancellationToken);
        var second = await children.AddChild(new ChildConfiguration
        {
            FirstName = "Byron", LastName = "Lovelace", TenantId = tenant.Id
        }, cancellationToken);
        await transactions.AddTransaction(new AllowanceTransaction
        {
            ChildId = first.Id, TenantId = tenant.Id, TransactionType = TransactionType.Deposit,
            TransactionAmount = 5m, Description = "First gift"
        }, cancellationToken);
        await transactions.AddTransaction(new AllowanceTransaction
        {
            ChildId = second.Id, TenantId = tenant.Id, TransactionType = TransactionType.Deposit,
            TransactionAmount = 7m, Description = "Second gift"
        }, cancellationToken);
        var shareLinks = new ShareLinkService(db, NullLogger<ShareLinkService>.Instance);

        var firstLink = await shareLinks.CreateAsync(tenant.Id, "Kitchen tablet", "parent@example.com", null, cancellationToken);
        var resolved = await shareLinks.ResolveAsync(firstLink.Token, cancellationToken);
        Assert.Equal(tenant.Id, resolved?.Tenant?.Id);
        var stored = await db.ShareLinks.SingleAsync(x => x.Id == firstLink.Link.Id, cancellationToken);
        Assert.NotEqual(firstLink.Token, stored.TokenHash);
        Assert.Matches("^[0-9a-f]{64}$", stored.TokenHash);
        Assert.Equal(2, (await children.GetChildrenWithBalance(resolved!.TenantId, cancellationToken)).Count());

        Assert.True(await shareLinks.RevokeAsync(firstLink.Link.Id, tenant.Id, cancellationToken));
        Assert.Null(await shareLinks.ResolveAsync(firstLink.Token, cancellationToken));

        var secondLink = await shareLinks.CreateAsync(tenant.Id, "E-ink frame", "parent@example.com", null, cancellationToken);
        Assert.NotNull(await shareLinks.ResolveAsync(secondLink.Token, cancellationToken));
        Assert.Null(await shareLinks.ResolveAsync(firstLink.Token, cancellationToken));

        var expiredLink = await shareLinks.CreateAsync(tenant.Id, "Expired frame", "parent@example.com", null, cancellationToken);
        expiredLink.Link.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync(cancellationToken);
        Assert.Null(await shareLinks.ResolveAsync(expiredLink.Token, cancellationToken));
    }

    private static (TenantService Tenants, UserService Users, ChildService Children, TransactionService Transactions)
        Services(AllowanceDbContext db, GlobalNotificationService? notifications = null)
    {
        notifications ??= new GlobalNotificationService();
        var transactions = new TransactionService(db, notifications);
        var children = new ChildService(
            db, notifications, transactions, NullLogger<ChildService>.Instance);
        return (
            new TenantService(db, NullLogger<TenantService>.Instance),
            new UserService(db, new MembershipService(db)),
            children,
            transactions);
    }

    private static DateTimeOffset LocalMidnightUtc(string? timeZoneId)
    {
        var zone = !string.IsNullOrWhiteSpace(timeZoneId) &&
                   TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var resolved)
            ? resolved
            : TimeZoneInfo.Utc;
        var localDate = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).Date;
        return new DateTimeOffset(localDate, zone.GetUtcOffset(localDate)).ToUniversalTime();
    }

    private sealed class TestJobExecutionContext(DateTimeOffset scheduled) : IJobExecutionContext
    {
        public IScheduler Scheduler => null!;
        public ITrigger Trigger => null!;
        public ICalendar Calendar => null!;
        public bool Recovering => false;
        public TriggerKey RecoveringTriggerKey => null!;
        public int RefireCount => 0;
        public JobDataMap MergedJobDataMap { get; } = new();
        public IJobDetail JobDetail => null!;
        public IJob JobInstance => null!;
        public DateTimeOffset FireTimeUtc => scheduled;
        public DateTimeOffset? ScheduledFireTimeUtc => scheduled;
        public DateTimeOffset? PreviousFireTimeUtc => null;
        public DateTimeOffset? NextFireTimeUtc => null;
        public string FireInstanceId => "test";
        public object? Result { get; set; }
        public TimeSpan JobRunTime => TimeSpan.Zero;
        public CancellationToken CancellationToken => default;
        public void Put(object key, object objectValue) => MergedJobDataMap[key.ToString()!] = objectValue;
        public object? Get(object key) => MergedJobDataMap[key.ToString()!];
    }
}
