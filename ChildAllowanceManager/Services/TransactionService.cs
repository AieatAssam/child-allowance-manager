using System.Data;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Data;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ChildAllowanceManager.Services;

public class TransactionService(
    AllowanceDbContext db,
    IGlobalNotificationService globalNotificationService) : ITransactionService
{
    private IQueryable<AllowanceTransaction> ForChild(string childId, string tenantId, bool ignoreDailyAllowance = false)
    {
        var query = db.Transactions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ChildId == childId && !x.Deleted);
        return ignoreDailyAllowance
            ? query.Where(x => x.TransactionType != TransactionType.DailyAllowance)
            : query;
    }

    public async ValueTask<IEnumerable<AllowanceTransaction>> GetTransactionsForChild(
        string childId, string tenantId, bool ignoreDailyAllowance = false,
        CancellationToken cancellationToken = default) =>
        await ForChild(childId, tenantId, ignoreDailyAllowance)
            .OrderByDescending(x => x.TransactionTimestamp).ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

    public async ValueTask<PagedResult<AllowanceTransaction>> GetPagedTransactionsForChild(
        string childId, string tenantId, int page, int pageSize,
        bool ignoreDailyAllowance = false, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = ForChild(childId, tenantId, ignoreDailyAllowance);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.TransactionTimestamp).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<AllowanceTransaction>(items, total, page, pageSize);
    }

    public async ValueTask<IEnumerable<BalanceHistoryEntry>> GetBalanceHistoryForChild(
        string childId, string tenantId, DateTimeOffset? startDate, DateTimeOffset? endDate,
        CancellationToken cancellationToken)
    {
        if (startDate is not null && endDate is not null && startDate > endDate)
            throw new ArgumentException("The start date must be before the end date.");

        IQueryable<AllowanceTransaction> query = ForChild(childId, tenantId);
        if (startDate is not null)
            query = query.Where(x => x.TransactionTimestamp >= startDate.Value);
        if (endDate is not null)
            query = query.Where(x => x.TransactionTimestamp <= endDate.Value);

        var result = await query.OrderBy(x => x.TransactionTimestamp).ThenBy(x => x.Id)
            .Select(x => new BalanceHistoryEntry(x.TransactionTimestamp, x.Balance))
            .ToListAsync(cancellationToken);
        if (result.Count == 0)
            return [];

        var firstDate = (startDate ?? result[0].Timestamp).UtcDateTime.Date;
        var lastDate = (endDate ?? result[^1].Timestamp).UtcDateTime.Date;
        var lastBalance = startDate is null
            ? 0m
            : await ForChild(childId, tenantId)
                .Where(x => x.TransactionTimestamp < startDate.Value)
                .OrderByDescending(x => x.TransactionTimestamp).ThenByDescending(x => x.Id)
                .Select(x => (decimal?)x.Balance)
                .FirstOrDefaultAsync(cancellationToken) ?? 0m;
        var extraRecords = new List<BalanceHistoryEntry>();
        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        {
            var existing = result.LastOrDefault(x => x.Timestamp.UtcDateTime.Date == date);
            if (existing is not null)
                lastBalance = existing.Balance;
            else
                extraRecords.Add(new BalanceHistoryEntry(new DateTimeOffset(date, TimeSpan.Zero), lastBalance));
        }

        return result.Concat(extraRecords).OrderBy(x => x.Timestamp).ToArray();
    }

    public async ValueTask<AllowanceTransaction?> GetLatestRegularTransactionForChild(
        string childId, string tenantId, CancellationToken cancellationToken = default) =>
        await ForChild(childId, tenantId)
            .Where(x => x.TransactionType == TransactionType.DailyAllowance ||
                        x.TransactionType == TransactionType.BirthdayAllowance)
            .OrderByDescending(x => x.TransactionTimestamp).ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async ValueTask<AllowanceTransaction?> GetLatestTransactionForChild(
        string childId, string tenantId, CancellationToken cancellationToken = default) =>
        await ForChild(childId, tenantId)
            .OrderByDescending(x => x.TransactionTimestamp).ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async ValueTask<decimal> GetBalanceForChild(
        string childId, string tenantId, CancellationToken cancellationToken = default) =>
        await ForChild(childId, tenantId)
            .OrderByDescending(x => x.TransactionTimestamp).ThenByDescending(x => x.Id)
            .Select(x => (decimal?)x.Balance)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;

    public async ValueTask<AllowanceTransaction> AddTransaction(
        AllowanceTransaction transaction, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transaction.ChildId) || string.IsNullOrWhiteSpace(transaction.TenantId))
            throw new ValidationException("A transaction must have a child and tenant.");
        if (string.IsNullOrWhiteSpace(transaction.Description))
            throw new ValidationException("A transaction description is required.");
        ValidateAmount(transaction);

        var childExists = await db.Children.AnyAsync(
            x => x.Id == transaction.ChildId && x.TenantId == transaction.TenantId && !x.Deleted,
            cancellationToken) || db.Children.Local.Any(
                x => x.Id == transaction.ChildId && x.TenantId == transaction.TenantId && !x.Deleted);
        if (!childExists)
            throw new KeyNotFoundException($"Child {transaction.ChildId} was not found in tenant {transaction.TenantId}.");

        await using var dbTransaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        if (transaction.AllowanceDate is not null)
        {
            var existing = await ForChild(transaction.ChildId, transaction.TenantId)
                .Where(x => x.AllowanceDate == transaction.AllowanceDate)
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
                return existing;
        }
        transaction.TransactionTimestamp = DateTimeOffset.UtcNow;
        transaction.CreatedTimestamp = transaction.TransactionTimestamp;
        transaction.UpdatedTimestamp = transaction.TransactionTimestamp;
        transaction.Balance = await GetBalanceForChild(transaction.ChildId, transaction.TenantId, cancellationToken)
            + transaction.TransactionAmount;

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        var message = transaction.TransactionType is TransactionType.DailyAllowance or TransactionType.BirthdayAllowance
            ? $"Added {transaction.TransactionAmount:C} for {transaction.Description.ToLowerInvariant()}"
            : transaction.TransactionType == TransactionType.Hold
            ? transaction.Description
            : $"Balance changed by {transaction.TransactionAmount:C} to {transaction.Balance:C}";
        globalNotificationService.OnChildStateChanged(transaction.ChildId, transaction.TenantId, message);
        return transaction;
    }

    private static void ValidateAmount(AllowanceTransaction transaction)
    {
        var valid = transaction.TransactionType switch
        {
            TransactionType.Deposit or TransactionType.DailyAllowance or TransactionType.BirthdayAllowance => transaction.TransactionAmount > 0,
            TransactionType.Withdrawal => transaction.TransactionAmount < 0,
            TransactionType.Hold => transaction.TransactionAmount == 0,
            _ => true
        };
        if (!valid)
            throw new ValidationException($"Amount does not match transaction type {transaction.TransactionType}.");
    }
}
