using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Data;
using Microsoft.EntityFrameworkCore;

namespace ChildAllowanceManager.Services;

public sealed class DevelopmentDataSeeder(AllowanceDbContext db)
{
    public const string TenantId = "development-tenant";
    public const string TenantSuffix = "demo";
    public const string UserEmail = "local@child-allowance.test";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await db.Tenants.AnyAsync(x => x.Id == TenantId, cancellationToken))
            return;

        var now = DateTimeOffset.UtcNow;
        var tenant = await db.Tenants.SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken);
        if (tenant is null)
        {
            tenant = new TenantConfiguration
            {
                Id = TenantId,
                TenantName = "Development Demo",
                UrlSuffix = TenantSuffix,
                CreatedTimestamp = now,
                UpdatedTimestamp = now
            };
            db.Tenants.Add(tenant);
        }

        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == UserEmail, cancellationToken);
        if (user is null)
        {
            user = new User
            {
                Email = UserEmail,
                Name = "Local Demo Parent",
                CreatedTimestamp = now,
                UpdatedTimestamp = now
            };
            db.Users.Add(user);
            user.Roles = [ValidRoles.Admin, ValidRoles.Parent];
            user.Tenants = [TenantId];
        }

        await AddChildIfMissingAsync(
            new ChildConfiguration
            {
                Id = "development-child-1",
                TenantId = TenantId,
                FirstName = "Alex",
                LastName = "Demo",
                RegularAllowance = 5m,
                BirthdayAllowance = 10m,
                BirthDate = new DateTime(2015, 6, 15)
            }, now, cancellationToken);
        await AddChildIfMissingAsync(
            new ChildConfiguration
            {
                Id = "development-child-2",
                TenantId = TenantId,
                FirstName = "Sam",
                LastName = "Demo",
                RegularAllowance = 3m,
                BirthdayAllowance = 8m,
                BirthDate = new DateTime(2016, 11, 2)
            }, now, cancellationToken);

        var history = new[]
        {
            ("development-transaction-4", "development-child-1", 5m, 5m, "Allowance saved", TransactionType.DailyAllowance, -28),
            ("development-transaction-5", "development-child-1", 5m, 10m, "Allowance saved", TransactionType.DailyAllowance, -24),
            ("development-transaction-6", "development-child-1", 5m, 15m, "Allowance saved", TransactionType.DailyAllowance, -20),
            ("development-transaction-7", "development-child-1", 5m, 20m, "Allowance saved", TransactionType.DailyAllowance, -16),
            ("development-transaction-8", "development-child-1", 5m, 25m, "Allowance saved", TransactionType.DailyAllowance, -12),
            ("development-transaction-9", "development-child-1", 5m, 30m, "Allowance saved", TransactionType.DailyAllowance, -8),
            ("development-transaction-10", "development-child-1", 5m, 35m, "Allowance saved", TransactionType.DailyAllowance, -4),
            ("development-transaction-2", "development-child-1", 5m, 35m, "Allowance saved", TransactionType.DailyAllowance, -2),
            ("development-transaction-1", "development-child-1", 5m, 40m, "Allowance saved", TransactionType.DailyAllowance, -1),
            ("development-transaction-11", "development-child-2", 3m, 3m, "Allowance received", TransactionType.DailyAllowance, -28),
            ("development-transaction-12", "development-child-2", 3m, 6m, "Allowance received", TransactionType.DailyAllowance, -25),
            ("development-transaction-13", "development-child-2", -4m, 2m, "Game purchase", TransactionType.Withdrawal, -23),
            ("development-transaction-14", "development-child-2", 3m, 5m, "Allowance received", TransactionType.DailyAllowance, -20),
            ("development-transaction-15", "development-child-2", -2m, 3m, "Snack", TransactionType.Withdrawal, -18),
            ("development-transaction-16", "development-child-2", 3m, 6m, "Allowance received", TransactionType.DailyAllowance, -15),
            ("development-transaction-17", "development-child-2", -5m, 1m, "Book", TransactionType.Withdrawal, -12),
            ("development-transaction-18", "development-child-2", 3m, 4m, "Allowance received", TransactionType.DailyAllowance, -9),
            ("development-transaction-19", "development-child-2", -3m, 1m, "Treat", TransactionType.Withdrawal, -7),
            ("development-transaction-20", "development-child-2", 3m, 4m, "Allowance received", TransactionType.DailyAllowance, -4),
            ("development-transaction-21", "development-child-2", -4m, 0m, "Toy", TransactionType.Withdrawal, -2),
            ("development-transaction-3", "development-child-2", 3m, 3m, "Allowance received", TransactionType.DailyAllowance, -1)
        };

        foreach (var transaction in history)
        {
            await SeedTransactionAsync(
                transaction.Item1,
                transaction.Item2,
                transaction.Item3,
                transaction.Item4,
                transaction.Item5,
                transaction.Item6,
                new DateTimeOffset(DateTime.UtcNow.Date.AddDays(transaction.Item7), TimeSpan.Zero),
                now,
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task AddChildIfMissingAsync(
        ChildConfiguration child, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await db.Children.AnyAsync(x => x.Id == child.Id, cancellationToken))
            return;

        child.CreatedTimestamp = now;
        child.UpdatedTimestamp = now;
        db.Children.Add(child);
    }

    private async Task SeedTransactionAsync(
        string id,
        string childId,
        decimal amount,
        decimal balance,
        string description,
        TransactionType transactionType,
        DateTimeOffset timestamp,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await db.Transactions.AnyAsync(x => x.Id == id, cancellationToken))
            return;

        db.Transactions.Add(new AllowanceTransaction
        {
            Id = id,
            ChildId = childId,
            TenantId = TenantId,
            Balance = balance,
            TransactionAmount = amount,
            Description = description,
            TransactionTimestamp = timestamp,
            TransactionType = transactionType,
            UpdatedTimestamp = now
        });
    }
}
