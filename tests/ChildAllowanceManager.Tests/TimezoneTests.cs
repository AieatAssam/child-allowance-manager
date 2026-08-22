using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Services;
using ChildAllowanceManager.Workers;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;

namespace ChildAllowanceManager.Tests;

public class TimezoneTests
{
    [Fact]
    public async Task Job_pays_a_family_at_its_own_local_midnight_not_utc()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        var notifications = new GlobalNotificationService();
        var tenants = new TenantService(db, NullLogger<TenantService>.Instance);
        var transactions = new TransactionService(db, notifications);
        var children = new ChildService(db, notifications, transactions, NullLogger<ChildService>.Instance);
        var tenant = await tenants.AddTenant(new TenantConfiguration
        {
            TenantName = "Pacific family", UrlSuffix = "pacific", TimeZoneId = "America/Los_Angeles"
        }, cancellationToken);
        var child = await children.AddChild(new ChildConfiguration
        {
            TenantId = tenant.Id, FirstName = "Child", LastName = "One", RegularAllowance = 5m
        }, cancellationToken);
        var localMidnight = LocalMidnightUtc(tenant.TimeZoneId);

        await new DailyAllowanceJob(transactions, children, tenants,
            NullLogger<DailyAllowanceJob>.Instance)
            .Execute(new TestJobExecutionContext(localMidnight));

        Assert.Equal(5m, await transactions.GetBalanceForChild(child.Id, tenant.Id, cancellationToken));
    }

    [Fact]
    public async Task Job_does_not_pay_when_the_scheduled_time_is_outside_the_local_first_hour()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        var notifications = new GlobalNotificationService();
        var tenants = new TenantService(db, NullLogger<TenantService>.Instance);
        var transactions = new TransactionService(db, notifications);
        var children = new ChildService(db, notifications, transactions, NullLogger<ChildService>.Instance);
        var tenant = await tenants.AddTenant(new TenantConfiguration
        {
            TenantName = "Pacific family", UrlSuffix = "pacific", TimeZoneId = "America/Los_Angeles"
        }, cancellationToken);
        var child = await children.AddChild(new ChildConfiguration
        {
            TenantId = tenant.Id, FirstName = "Child", LastName = "One", RegularAllowance = 5m
        }, cancellationToken);

        await new DailyAllowanceJob(transactions, children, tenants,
            NullLogger<DailyAllowanceJob>.Instance)
            .Execute(new TestJobExecutionContext(LocalMidnightUtc(tenant.TimeZoneId).AddHours(2)));

        Assert.Equal(0m, await transactions.GetBalanceForChild(child.Id, tenant.Id, cancellationToken));
    }

    [Fact]
    public async Task Unknown_timezone_falls_back_to_utc_without_skipping_other_families()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        var notifications = new GlobalNotificationService();
        var tenants = new TenantService(db, NullLogger<TenantService>.Instance);
        var transactions = new TransactionService(db, notifications);
        var children = new ChildService(db, notifications, transactions, NullLogger<ChildService>.Instance);
        var tenant = new TenantConfiguration
        {
            Id = "tenant-1", TenantName = "UTC family", UrlSuffix = "utc-family", TimeZoneId = "not-a-time-zone"
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);
        var child = await children.AddChild(new ChildConfiguration
        {
            TenantId = tenant.Id, FirstName = "Child", LastName = "One", RegularAllowance = 5m
        }, cancellationToken);

        await new DailyAllowanceJob(transactions, children, tenants,
            NullLogger<DailyAllowanceJob>.Instance)
            .Execute(new TestJobExecutionContext(
                new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero)));

        Assert.Equal(5m, await transactions.GetBalanceForChild(child.Id, tenant.Id, cancellationToken));
    }

    [Fact]
    public async Task Next_allowance_date_remains_future_after_today_was_paid()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await PostgresTestDatabase.CreateCleanContextAsync(cancellationToken);
        var tenant = new TenantConfiguration { Id = "tenant-1", TenantName = "Family", UrlSuffix = "family" };
        var child = new ChildConfiguration { TenantId = tenant.Id, FirstName = "Child", LastName = "One" };
        db.AddRange(tenant, child);
        await db.SaveChangesAsync(cancellationToken);
        var allowance = new AllowanceTransaction
        {
            ChildId = child.Id, TenantId = tenant.Id, TransactionType = TransactionType.DailyAllowance,
            TransactionAmount = 1m, Balance = 1m, Description = "Daily allowance",
            AllowanceDate = DateTime.UtcNow.Date, TransactionTimestamp = DateTimeOffset.UtcNow
        };
        db.Transactions.Add(allowance);
        await db.SaveChangesAsync(cancellationToken);
        var result = Assert.Single(await new ChildService(db, new GlobalNotificationService(),
            new TransactionService(db, new GlobalNotificationService()),
            NullLogger<ChildService>.Instance).GetChildrenWithBalance(tenant.Id, cancellationToken));

        Assert.True(result.NextRegularChangeDate > DateTimeOffset.UtcNow);
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
