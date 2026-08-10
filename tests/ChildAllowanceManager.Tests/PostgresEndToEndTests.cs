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
        await using var db = await PostgresTestDatabase.CreateMigratedContextAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.True(await db.Database.CanConnectAsync());
    }

    [Fact]
    public async Task DevelopmentSeederCreatesReusableDemoWorkspace()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var seeder = new DevelopmentDataSeeder(db);

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        var tenant = await db.Tenants.SingleAsync(x => x.Id == DevelopmentDataSeeder.TenantId);
        var user = await db.Users.SingleAsync(x => x.Email == DevelopmentDataSeeder.UserEmail);

        Assert.Equal("demo", tenant.UrlSuffix);
        Assert.Equal([DevelopmentDataSeeder.TenantId], user.Tenants);
        Assert.Contains(ValidRoles.Admin, user.Roles);
        Assert.Contains(ValidRoles.Parent, user.Roles);
        Assert.Equal(2, await db.Children.CountAsync(x => x.TenantId == DevelopmentDataSeeder.TenantId));
        Assert.Equal(21, await db.Transactions.CountAsync(x => x.TenantId == DevelopmentDataSeeder.TenantId));
        Assert.Equal(40m, (await db.Transactions.SingleAsync(x => x.Id == "development-transaction-1")).Balance);
        Assert.Equal(3m, (await db.Transactions.SingleAsync(x => x.Id == "development-transaction-3")).Balance);
    }

    [Fact]
    public async Task SeededDemoTenantSupportsParentActionsAndManualDailyWorkerRun()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var notifications = new GlobalNotificationService();
        var (tenants, _, children, transactions) = Services(db, notifications);
        await new DevelopmentDataSeeder(db).SeedAsync();

        var demo = await tenants.GetTenantBySuffix(DevelopmentDataSeeder.TenantSuffix);
        Assert.NotNull(demo);
        var seededChildren = (await children.GetChildrenWithBalance(demo!.Id, default)).ToArray();
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
        });
        await transactions.AddTransaction(new AllowanceTransaction
        {
            ChildId = sam.Id,
            TenantId = demo.Id,
            TransactionType = TransactionType.Withdrawal,
            TransactionAmount = -1m,
            Description = "Small treat"
        });
        Assert.Equal(47m, await transactions.GetBalanceForChild(alex.Id, demo.Id));
        Assert.Equal(2m, await transactions.GetBalanceForChild(sam.Id, demo.Id));
        Assert.Equal(6, (await transactions.GetPagedTransactionsForChild(
            sam.Id, demo.Id, 1, 25, ignoreDailyAllowance: true)).Total);

        var alexConfiguration = (await children.GetChild(alex.Id, demo.Id))!;
        alexConfiguration.HoldDaysRemaining = 1;
        await children.UpdateChild(alexConfiguration);

        var job = new DailyAllowanceJob(
            transactions, children, tenants,
            NullLogger<DailyAllowanceJob>.Instance);
        await job.Execute(new TestJobExecutionContext(DateTimeOffset.UtcNow));

        Assert.Equal(47m, await transactions.GetBalanceForChild(alex.Id, demo.Id));
        Assert.Equal(5m, await transactions.GetBalanceForChild(sam.Id, demo.Id));
        Assert.Equal(0, (await children.GetChild(alex.Id, demo.Id))!.HoldDaysRemaining);
    }

    [Fact]
    public async Task FullLifecycleKeepsServicesAndStoredDataConsistent()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var (tenants, users, children, transactions) = Services(db);

        var tenant = await tenants.AddTenant(new TenantConfiguration
        {
            TenantName = "The Lovelace Family",
            UrlSuffix = "lovelace"
        });
        Assert.Equal(tenant.Id, (await tenants.GetTenantBySuffix("LOVELACE"))!.Id);
        tenant.TenantName = "Lovelace Home";
        await tenants.UpdateTenant(tenant);
        Assert.Equal("Lovelace Home", (await tenants.GetTenant(tenant.Id))!.TenantName);
        Assert.Single(await tenants.GetTenants());

        var admin = await users.InitializeUserAsync(
            " Admin@Example.com ", "Ada", tenant.Id, default);
        await users.AddUserToTenantAsync(
            "parent@example.com", "Charles", tenant.Id, ValidRoles.Parent, default);
        var parent = await users.GetUserByEmailAsync("PARENT@EXAMPLE.COM", default);
        parent!.Name = "Charles Babbage";
        await users.UpsertUserAsync(parent, default);
        Assert.Contains(ValidRoles.Admin, admin.Roles);
        Assert.Single(await users.GetTenantUsersInRole(tenant.Id, ValidRoles.Parent, default));
        Assert.Equal(2, (await users.GetUsersAsync(default)).Count());

        var child = await children.AddChild(new ChildConfiguration
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            TenantId = tenant.Id,
            RegularAllowance = 5m
        });
        child.FirstName = "Augusta";
        child.HoldDaysRemaining = 1;
        await children.UpdateChild(child);
        Assert.Equal("Augusta", (await children.GetChild(child.Id, tenant.Id))!.FirstName);
        Assert.Single(await children.GetChildren(tenant.Id));

        await transactions.AddTransaction(new AllowanceTransaction
        {
            ChildId = child.Id,
            TenantId = tenant.Id,
            TransactionType = TransactionType.Hold,
            Description = "School holiday (1 day)"
        });
        await transactions.AddTransaction(new AllowanceTransaction
        {
            ChildId = child.Id,
            TenantId = tenant.Id,
            TransactionType = TransactionType.DailyAllowance,
            TransactionAmount = 5m,
            Description = "Daily allowance"
        });
        await transactions.AddTransaction(new AllowanceTransaction
        {
            ChildId = child.Id,
            TenantId = tenant.Id,
            TransactionType = TransactionType.Deposit,
            TransactionAmount = 10m,
            Description = "Birthday gift"
        });
        await transactions.AddTransaction(new AllowanceTransaction
        {
            ChildId = child.Id,
            TenantId = tenant.Id,
            TransactionType = TransactionType.Withdrawal,
            TransactionAmount = -3m,
            Description = "Book"
        });

        Assert.Equal(12m, await transactions.GetBalanceForChild(child.Id, tenant.Id));
        Assert.Equal(5, (await transactions.GetTransactionsForChild(child.Id, tenant.Id)).Count());
        var page = await transactions.GetPagedTransactionsForChild(child.Id, tenant.Id, 0, 200);
        var withoutDaily = await transactions.GetPagedTransactionsForChild(
            child.Id, tenant.Id, 1, 100, true);
        Assert.Equal(5, page.Total);
        Assert.Equal(100, page.PageSize);
        Assert.Equal(4, withoutDaily.Total);
        Assert.Equal(TransactionType.DailyAllowance,
            (await transactions.GetLatestRegularTransactionForChild(child.Id, tenant.Id))!.TransactionType);
        Assert.Equal(TransactionType.Withdrawal,
            (await transactions.GetLatestTransactionForChild(child.Id, tenant.Id))!.TransactionType);
        Assert.Single(await children.GetChildrenWithBalance(tenant.Id, default));
        Assert.Single(await children.GetChildrenWithBalanceHistory(tenant.Id, null, null, default));

        await users.DeleteUserAsync("parent@example.com", default);
        Assert.Null(await users.GetUserByEmailAsync("parent@example.com", default));
        Assert.True(await children.DeleteChild(child.Id, tenant.Id));
        Assert.Null(await children.GetChild(child.Id, tenant.Id));
        Assert.Equal(5, await db.Transactions.CountAsync(x => x.ChildId == child.Id));

        var retainedChild = await children.AddChild(new ChildConfiguration
        {
            FirstName = "Byron",
            LastName = "Lovelace",
            TenantId = tenant.Id
        });
        Assert.True(await tenants.DeleteTenant(tenant.Id));
        Assert.Null(await tenants.GetTenant(tenant.Id));
        Assert.Empty(await children.GetChildren(tenant.Id));
        Assert.True((await db.Children.FindAsync(retainedChild.Id))!.Deleted);
        Assert.Empty((await users.GetUserByEmailAsync("admin@example.com", default))!.Tenants);
    }

    [Fact]
    public async Task DailyWorkerAppliesDueAndBirthdayAllowancesAndConsumesHolds()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var notifications = new GlobalNotificationService();
        var (tenants, _, children, transactions) = Services(db, notifications);
        var messages = new List<string>();
        notifications.ChildStateChanged += (_, args) => messages.Add(args.NotificationMessage);
        var tenant = await tenants.AddTenant(new TenantConfiguration
        {
            TenantName = "Worker Family",
            UrlSuffix = "worker"
        });

        var due = await children.AddChild(new ChildConfiguration
        {
            FirstName = "Due",
            LastName = "Child",
            TenantId = tenant.Id,
            RegularAllowance = 5m
        });
        var birthday = await children.AddChild(new ChildConfiguration
        {
            FirstName = "Birthday",
            LastName = "Child",
            TenantId = tenant.Id,
            BirthDate = DateTime.Today,
            RegularAllowance = 5m,
            BirthdayAllowance = 20m
        });
        var held = await children.AddChild(new ChildConfiguration
        {
            FirstName = "Held",
            LastName = "Child",
            TenantId = tenant.Id,
            RegularAllowance = 3m,
            HoldDaysRemaining = 1
        });

        var job = new DailyAllowanceJob(
            transactions, children, tenants,
            NullLogger<DailyAllowanceJob>.Instance);
        var now = DateTimeOffset.UtcNow;
        await job.Execute(new TestJobExecutionContext(now));

        Assert.Equal(TransactionType.DailyAllowance,
            (await transactions.GetLatestTransactionForChild(due.Id, tenant.Id))!.TransactionType);
        var birthdayTransaction = await transactions.GetLatestTransactionForChild(birthday.Id, tenant.Id);
        Assert.Equal(TransactionType.BirthdayAllowance, birthdayTransaction!.TransactionType);
        Assert.Equal(20m, birthdayTransaction.TransactionAmount);
        Assert.Equal(5m, await transactions.GetBalanceForChild(due.Id, tenant.Id));
        Assert.Equal(20m, await transactions.GetBalanceForChild(birthday.Id, tenant.Id));
        Assert.DoesNotContain(
            (await transactions.GetTransactionsForChild(held.Id, tenant.Id)),
            x => x.TransactionType != TransactionType.Adjustment);
        Assert.Equal(0, (await children.GetChild(held.Id, tenant.Id))!.HoldDaysRemaining);
        Assert.Contains(messages, message => message.Contains("daily allowance"));
        Assert.Contains(messages, message => message.Contains("birthday allowance"));
    }

    [Fact]
    public async Task TenantAndChildQueriesCannotCrossTenantBoundariesAndBalancesReconcile()
    {
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync();
        var notifications = new GlobalNotificationService();
        var (tenants, _, children, transactions) = Services(db, notifications);
        var firstTenant = await tenants.AddTenant(new TenantConfiguration
        {
            TenantName = "First Family",
            UrlSuffix = "first"
        });
        var secondTenant = await tenants.AddTenant(new TenantConfiguration
        {
            TenantName = "Second Family",
            UrlSuffix = "second"
        });
        var firstChild = await children.AddChild(new ChildConfiguration
        {
            FirstName = "First",
            LastName = "Child",
            TenantId = firstTenant.Id
        });
        var secondChild = await children.AddChild(new ChildConfiguration
        {
            FirstName = "Second",
            LastName = "Child",
            TenantId = secondTenant.Id
        });
        await transactions.AddTransaction(new AllowanceTransaction
        {
            ChildId = firstChild.Id,
            TenantId = firstTenant.Id,
            TransactionType = TransactionType.Deposit,
            TransactionAmount = 7m,
            Description = "First deposit"
        });
        await transactions.AddTransaction(new AllowanceTransaction
        {
            ChildId = secondChild.Id,
            TenantId = secondTenant.Id,
            TransactionType = TransactionType.Deposit,
            TransactionAmount = 11m,
            Description = "Second deposit"
        });

        Assert.Null(await children.GetChild(firstChild.Id, secondTenant.Id));
        Assert.DoesNotContain(firstChild.Id, (await children.GetChildren(secondTenant.Id)).Select(child => child.Id));
        Assert.Empty(await transactions.GetTransactionsForChild(firstChild.Id, secondTenant.Id));
        Assert.Equal(0m, await transactions.GetBalanceForChild(firstChild.Id, secondTenant.Id));

        foreach (var child in new[] { firstChild, secondChild })
        {
            var ordered = (await transactions.GetTransactionsForChild(child.Id, child.TenantId))
                .OrderBy(x => x.TransactionTimestamp)
                .ToArray();
            var balance = 0m;
            foreach (var transaction in ordered)
            {
                balance += transaction.TransactionAmount;
                Assert.Equal(balance, transaction.Balance);
            }
            Assert.Equal(balance, await transactions.GetBalanceForChild(child.Id, child.TenantId));
        }

        var duplicate = new TenantConfiguration
        {
            TenantName = "Duplicate",
            UrlSuffix = "first"
        };
        db.Tenants.Add(duplicate);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static (TenantService Tenants, UserService Users, ChildService Children, TransactionService Transactions)
        Services(AllowanceDbContext db, GlobalNotificationService? notifications = null)
    {
        notifications ??= new GlobalNotificationService();
        var transactions = new TransactionService(db, notifications);
        var children = new ChildService(
            db, notifications, transactions, NullLogger<ChildService>.Instance);
        return (
            new TenantService(db, children, NullLogger<TenantService>.Instance),
            new UserService(db),
            children,
            transactions);
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
