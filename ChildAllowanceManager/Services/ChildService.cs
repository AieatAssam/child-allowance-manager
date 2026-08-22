using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Common.Validators;
using ChildAllowanceManager.Data;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ChildAllowanceManager.Services;

public class ChildService(
    AllowanceDbContext db,
    IGlobalNotificationService globalNotificationService,
    ITransactionService transactionService,
    ILogger<ChildService> logger) : IChildService
{
    private readonly ChildConfigurationValidator validator = new();

    public async ValueTask<IEnumerable<ChildConfiguration>> GetChildren(string tenantId, CancellationToken cancellationToken = default) =>
        await db.Children.AsNoTracking()
            .Where(child => child.TenantId == tenantId && !child.Deleted)
            .OrderBy(child => child.FirstName)
            .ToListAsync(cancellationToken);

    public async ValueTask<IEnumerable<ChildWithBalanceHistory>> GetChildrenWithBalanceHistory(
        string tenantId, DateTimeOffset? startDate, DateTimeOffset? endDate, CancellationToken cancellationToken)
    {
        var children = (await GetChildren(tenantId, cancellationToken)).ToArray();
        if (startDate is null && endDate is null)
        {
            var childIds = children.Select(child => child.Id).ToArray();
            var transactions = await db.Transactions.AsNoTracking()
                .Where(transaction => transaction.TenantId == tenantId &&
                                      !transaction.Deleted && childIds.Contains(transaction.ChildId))
                .OrderBy(transaction => transaction.ChildId)
                .ThenBy(transaction => transaction.TransactionTimestamp)
                .ThenBy(transaction => transaction.Id)
                .Select(transaction => new
                {
                    transaction.ChildId,
                    Entry = new BalanceHistoryEntry(transaction.TransactionTimestamp, transaction.Balance)
                })
                .ToListAsync(cancellationToken);
            var historyByChild = transactions
                .GroupBy(transaction => transaction.ChildId)
                .ToDictionary(group => group.Key, group => FillHistory(group.Select(transaction => transaction.Entry)));

            return children.Select(child => new ChildWithBalanceHistory(
                child.Id,
                $"{child.FirstName} {child.LastName}",
                child.TenantId,
                historyByChild.GetValueOrDefault(child.Id, [])));
        }

        var result = new List<ChildWithBalanceHistory>();
        foreach (var child in children)
        {
            var history = await transactionService.GetBalanceHistoryForChild(
                child.Id, tenantId, startDate, endDate, cancellationToken);
            result.Add(new ChildWithBalanceHistory(child.Id, $"{child.FirstName} {child.LastName}", child.TenantId, history.ToArray()));
        }

        return result;
    }

    public async ValueTask<IEnumerable<ChildWithBalance>> GetChildrenWithBalance(
        string tenantId, CancellationToken cancellationToken)
    {
        var children = (await GetChildren(tenantId, cancellationToken)).ToArray();
        var childIds = children.Select(child => child.Id).ToArray();
        var latestBalances = await db.Transactions.AsNoTracking()
            .Where(transaction => transaction.TenantId == tenantId &&
                                  !transaction.Deleted && childIds.Contains(transaction.ChildId))
            .GroupBy(transaction => transaction.ChildId)
            .Select(group => group
                .OrderByDescending(transaction => transaction.TransactionTimestamp)
                .ThenByDescending(transaction => transaction.Id)
                .Select(transaction => new { transaction.ChildId, transaction.Balance })
                .First())
            .ToDictionaryAsync(item => item.ChildId, item => item.Balance, cancellationToken);
        var tenantTimeZoneId = await db.Tenants.AsNoTracking()
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => tenant.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken);
        var zone = ResolveZone(tenantTimeZoneId);
        var result = new List<ChildWithBalance>();
        foreach (var child in children)
        {
            var balance = latestBalances.GetValueOrDefault(child.Id);
            var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
            var today = localNow.Date;
            var nextLocalDate = today.AddDays(1 + child.HoldDaysRemaining);

            var isBirthday = child.BirthDate is not null &&
                SameDayInYear(today, child.BirthDate.Value.Date);

            result.Add(new ChildWithBalance
            {
                Id = child.Id,
                TenantId = child.TenantId,
                Balance = balance,
                Name = $"{child.FirstName} {child.LastName}",
                HoldDaysRemaining = child.HoldDaysRemaining,
                IsBirthday = isBirthday,
                NextRegularChange = isBirthday && child.BirthdayAllowance is not null
                    ? child.BirthdayAllowance.Value
                    : child.RegularAllowance,
                NextRegularChangeDate = new DateTimeOffset(nextLocalDate, zone.GetUtcOffset(nextLocalDate)),
                TimeZoneId = tenantTimeZoneId ?? "Europe/London",
                NextRegularChangeLocalDate = DateOnly.FromDateTime(nextLocalDate)
            });
        }

        return result;
    }

    private static BalanceHistoryEntry[] FillHistory(IEnumerable<BalanceHistoryEntry> entries)
    {
        var orderedEntries = entries.OrderBy(x => x.Timestamp).ToArray();
        if (orderedEntries.Length == 0)
            return [];

        var firstDate = orderedEntries[0].Timestamp.UtcDateTime.Date;
        var lastDate = orderedEntries[^1].Timestamp.UtcDateTime.Date;
        var lastBalance = 0m;
        var entryIndex = 0;
        var result = new List<BalanceHistoryEntry>();
        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        {
            BalanceHistoryEntry? lastEntryForDate = null;
            while (entryIndex < orderedEntries.Length &&
                   orderedEntries[entryIndex].Timestamp.UtcDateTime.Date == date)
                lastEntryForDate = orderedEntries[entryIndex++];

            if (lastEntryForDate is not null)
            {
                lastBalance = lastEntryForDate.Balance;
                result.Add(lastEntryForDate);
            }
            else
                result.Add(new BalanceHistoryEntry(new DateTimeOffset(date, TimeSpan.Zero), lastBalance));
        }

        return result.ToArray();
    }

    private static bool SameDayInYear(DateTime? first, DateTime? second) =>
        first is not null && second is not null &&
        first.Value.Month == second.Value.Month && first.Value.Day == second.Value.Day;

    private static TimeZoneInfo ResolveZone(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id) &&
            TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
            return zone;
        return TimeZoneInfo.Utc;
    }

    public async ValueTask<ChildConfiguration> AddChild(ChildConfiguration child, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(child, cancellationToken);
        child.CreatedTimestamp = DateTimeOffset.UtcNow;
        child.UpdatedTimestamp = child.CreatedTimestamp;
        db.Children.Add(child);
        await transactionService.AddTransaction(new AllowanceTransaction
        {
            ChildId = child.Id,
            TenantId = child.TenantId,
            TransactionType = TransactionType.Adjustment,
            Description = "Initial balance"
        }, cancellationToken);
        return child;
    }

    public async ValueTask<ChildConfiguration> UpdateChild(ChildConfiguration child, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(child, cancellationToken);
        var existing = await db.Children.FirstOrDefaultAsync(
            x => x.Id == child.Id && x.TenantId == child.TenantId && !x.Deleted, cancellationToken);
        if (existing is null)
            throw new KeyNotFoundException($"Child {child.Id} was not found in tenant {child.TenantId}.");

        existing.FirstName = child.FirstName;
        existing.LastName = child.LastName;
        existing.BirthDate = child.BirthDate;
        existing.RegularAllowance = child.RegularAllowance;
        existing.HoldDaysRemaining = Math.Max(0, child.HoldDaysRemaining);
        existing.BirthdayAllowance = child.BirthdayAllowance;

        existing.UpdatedTimestamp = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        globalNotificationService.OnChildStateChanged(existing.Id, existing.TenantId, string.Empty);
        return existing;
    }

    public async ValueTask<ChildConfiguration> ApplyHoldAsync(
        string childId, string tenantId, int days, string description, string? requestId,
        CancellationToken cancellationToken = default)
    {
        if (days <= 0)
            throw new ValidationException("Hold days must be greater than zero.");

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var child = await db.Children.FirstOrDefaultAsync(
            x => x.Id == childId && x.TenantId == tenantId && !x.Deleted, cancellationToken)
            ?? throw new KeyNotFoundException($"Child {childId} was not found in tenant {tenantId}.");

        child.HoldDaysRemaining = Math.Max(0, child.HoldDaysRemaining + days);
        child.UpdatedTimestamp = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await transactionService.AddTransaction(new AllowanceTransaction
        {
            ChildId = childId,
            TenantId = tenantId,
            TransactionAmount = 0,
            TransactionType = TransactionType.Hold,
            Description = string.IsNullOrWhiteSpace(description) ? string.Empty : $"{description} ({days} days)",
            RequestId = requestId
        }, cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        globalNotificationService.OnChildStateChanged(child.Id, child.TenantId, description);
        return child;
    }

    public async ValueTask<ChildConfiguration> RemoveHoldDayAsync(
        string childId, string tenantId, string? requestId, CancellationToken cancellationToken = default)
    {
        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var child = await db.Children.FirstOrDefaultAsync(
            x => x.Id == childId && x.TenantId == tenantId && !x.Deleted, cancellationToken)
            ?? throw new KeyNotFoundException($"Child {childId} was not found in tenant {tenantId}.");

        child.HoldDaysRemaining = Math.Max(0, child.HoldDaysRemaining - 1);
        child.UpdatedTimestamp = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await transactionService.AddTransaction(new AllowanceTransaction
        {
            ChildId = childId,
            TenantId = tenantId,
            TransactionAmount = 0,
            TransactionType = TransactionType.Hold,
            Description = "Hold reduced by 1 day",
            RequestId = requestId
        }, cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        globalNotificationService.OnChildStateChanged(child.Id, child.TenantId, "Hold reduced by 1 day");
        return child;
    }

    private async Task ValidateAsync(ChildConfiguration child, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(child, cancellationToken);
        if (!result.IsValid)
            throw new ValidationException(result.Errors);
    }

    public async ValueTask<bool> DeleteChild(string id, string tenantId, CancellationToken cancellationToken = default)
    {
        var child = await db.Children.FirstOrDefaultAsync(
            item => item.Id == id && item.TenantId == tenantId, cancellationToken);
        if (child is null)
        {
            logger.LogWarning("Trying to delete child with id {Id} that does not exist", id);
            return false;
        }

        if (child.Deleted)
        {
            logger.LogWarning("Child {Id} is already deleted", id);
            return true;
        }

        child.Deleted = true;
        child.UpdatedTimestamp = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async ValueTask<IEnumerable<ChildConfiguration>> GetDeletedChildren(
        string tenantId, CancellationToken cancellationToken = default) =>
        await db.Children.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Deleted)
            .OrderBy(x => x.FirstName)
            .ToListAsync(cancellationToken);

    public async ValueTask<bool> RestoreChild(
        string id, string tenantId, CancellationToken cancellationToken = default)
    {
        var child = await db.Children.FirstOrDefaultAsync(
            x => x.Id == id && x.TenantId == tenantId && x.Deleted, cancellationToken);
        if (child is null)
            return false;
        if (await db.Tenants.AnyAsync(x => x.Id == tenantId && x.Deleted, cancellationToken))
            throw new InvalidOperationException("Restore the family first.");

        child.Deleted = false;
        child.UpdatedTimestamp = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async ValueTask<ChildConfiguration?> GetChild(
        string childId, string childTenantId, CancellationToken cancellationToken = default) =>
        await db.Children.AsNoTracking().FirstOrDefaultAsync(
            child => child.Id == childId && child.TenantId == childTenantId && !child.Deleted,
            cancellationToken);
}
