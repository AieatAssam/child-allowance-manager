using System.Data;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Data;
using Microsoft.EntityFrameworkCore;

namespace ChildAllowanceManager.Services;

public class TransactionService(
    AllowanceDbContext db,
    IGlobalNotificationService globalNotificationService) : ITransactionService
{
    private IQueryable<AllowanceTransaction> ForChild(string childId, string tenantId, bool ignoreDailyAllowance = false)
    {
        var query = db.Transactions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ChildId == childId);
        return ignoreDailyAllowance
            ? query.Where(x => x.TransactionType != TransactionType.DailyAllowance)
            : query;
    }

    public async ValueTask<IEnumerable<AllowanceTransaction>> GetTransactionsForChild(
        string childId, string tenantId, bool ignoreDailyAllowance = false,
        CancellationToken cancellationToken = default) =>
        await ForChild(childId, tenantId, ignoreDailyAllowance)
            .OrderByDescending(x => x.TransactionTimestamp)
            .ToListAsync(cancellationToken);

    public async ValueTask<PagedResult<AllowanceTransaction>> GetPagedTransactionsForChild(
        string childId, string tenantId, int page, int pageSize,
        bool ignoreDailyAllowance = false, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = ForChild(childId, tenantId, ignoreDailyAllowance);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.TransactionTimestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<AllowanceTransaction>(items, total, page, pageSize);
    }

    public async ValueTask<IEnumerable<BalanceHistoryEntry>> GetBalanceHistoryForChild(
        string childId, string tenantId, DateTimeOffset? startDate, DateTimeOffset? endDate,
        CancellationToken cancellationToken)
    {
        IQueryable<AllowanceTransaction> query = ForChild(childId, tenantId);
        if (startDate is not null)
            query = query.Where(x => x.TransactionTimestamp >= startDate.Value);
        if (endDate is not null)
            query = query.Where(x => x.TransactionTimestamp <= endDate.Value);

        var result = (await query.OrderBy(x => x.TransactionTimestamp)
            .Select(x => new BalanceHistoryEntry(x.TransactionTimestamp, x.Balance))
            .ToListAsync(cancellationToken));
        if (result.Count < 2)
            return result;

        var firstDate = (startDate ?? result[0].Timestamp).Date;
        var lastDate = (endDate ?? result[^1].Timestamp).Date;
        var lastBalance = result[0].Balance;
        var extraRecords = new List<BalanceHistoryEntry>();
        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        {
            var existing = result.LastOrDefault(x => x.Timestamp.Date == date);
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
            .OrderByDescending(x => x.TransactionTimestamp)
            .FirstOrDefaultAsync(cancellationToken);

    public async ValueTask<AllowanceTransaction?> GetLatestTransactionForChild(
        string childId, string tenantId, CancellationToken cancellationToken = default) =>
        await ForChild(childId, tenantId)
            .OrderByDescending(x => x.TransactionTimestamp)
            .FirstOrDefaultAsync(cancellationToken);

    public async ValueTask<decimal> GetBalanceForChild(
        string childId, string tenantId, CancellationToken cancellationToken = default) =>
        await ForChild(childId, tenantId)
            .OrderByDescending(x => x.TransactionTimestamp)
            .Select(x => (decimal?)x.Balance)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;

    public async ValueTask<AllowanceTransaction> AddTransaction(
        AllowanceTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using var dbTransaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        transaction.TransactionTimestamp = DateTimeOffset.UtcNow;
        transaction.CreatedTimestamp = transaction.TransactionTimestamp;
        transaction.UpdatedTimestamp = transaction.TransactionTimestamp;
        transaction.Balance = await GetBalanceForChild(transaction.ChildId, transaction.TenantId, cancellationToken)
            + transaction.TransactionAmount;

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        var message = transaction.TransactionType == TransactionType.Hold
            ? transaction.Description
            : $"Balance changed by {transaction.TransactionAmount:C} to {transaction.Balance:C}";
        globalNotificationService.OnChildStateChanged(transaction.ChildId, transaction.TenantId, message);
        return transaction;
    }
}
