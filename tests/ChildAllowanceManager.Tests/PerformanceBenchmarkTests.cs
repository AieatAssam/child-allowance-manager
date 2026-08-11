using System.Data.Common;
using System.Diagnostics;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Data;
using ChildAllowanceManager.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChildAllowanceManager.Tests;

public sealed class PerformanceBenchmarkTests(ITestOutputHelper output)
{
    [Fact]
    public async Task DashboardServiceBenchmark()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings__Postgres is required.");
        var counter = new DbCommandCounter();
        var options = new DbContextOptionsBuilder<AllowanceDbContext>()
            .UseNpgsql(connection)
            .AddInterceptors(counter)
            .Options;

        await using var db = new AllowanceDbContext(options);
        await db.Database.EnsureDeletedAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);

        const int childCount = 50;
        const int transactionsPerChild = 10;
        var now = DateTimeOffset.UtcNow;
        var children = Enumerable.Range(0, childCount)
            .Select(i => new ChildConfiguration
            {
                Id = $"benchmark-child-{i}",
                TenantId = "benchmark-tenant",
                FirstName = $"Child{i}",
                LastName = "Benchmark"
            })
            .ToArray();
        db.Tenants.Add(new TenantConfiguration
        {
            Id = "benchmark-tenant",
            TenantName = "Benchmark",
            UrlSuffix = "benchmark"
        });
        db.Children.AddRange(children);
        db.Transactions.AddRange(children.SelectMany(child => Enumerable.Range(0, transactionsPerChild).Select(i => new AllowanceTransaction
        {
            ChildId = child.Id,
            TenantId = child.TenantId,
            TransactionType = TransactionType.Deposit,
            TransactionAmount = 1m,
            Balance = i + 1,
            Description = "Benchmark",
            TransactionTimestamp = now.AddMinutes(-transactionsPerChild + i),
            CreatedTimestamp = now,
            UpdatedTimestamp = now
        })));
        await db.SaveChangesAsync(cancellationToken);

        var service = new ChildService(
            db,
            new GlobalNotificationService(),
            new TransactionService(db, new GlobalNotificationService()),
            NullLogger<ChildService>.Instance);

        await service.GetChildrenWithBalance("benchmark-tenant", cancellationToken);
        await service.GetChildrenWithBalanceHistory("benchmark-tenant", null, null, cancellationToken);

        counter.Reset();
        var balanceTimer = Stopwatch.StartNew();
        var balances = (await service.GetChildrenWithBalance("benchmark-tenant", cancellationToken)).ToArray();
        balanceTimer.Stop();
        var balanceQueries = counter.Count;

        counter.Reset();
        var historyTimer = Stopwatch.StartNew();
        var histories = (await service.GetChildrenWithBalanceHistory("benchmark-tenant", null, null, cancellationToken)).ToArray();
        historyTimer.Stop();
        var historyQueries = counter.Count;

        output.WriteLine($"children={childCount}, transactions_per_child={transactionsPerChild}");
        output.WriteLine($"balances: {balanceTimer.Elapsed.TotalMilliseconds:F1} ms, {balanceQueries} reader commands, {balances.Length} rows");
        output.WriteLine($"history: {historyTimer.Elapsed.TotalMilliseconds:F1} ms, {historyQueries} reader commands, {histories.Length} rows");

        Assert.Equal(childCount, balances.Length);
        Assert.Equal(childCount, histories.Length);
    }

    private sealed class DbCommandCounter : DbCommandInterceptor
    {
        private int count;

        public int Count => Volatile.Read(ref count);

        public void Reset() => Interlocked.Exchange(ref count, 0);

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref count);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref count);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
