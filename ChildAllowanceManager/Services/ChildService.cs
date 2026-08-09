using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Data;
using Microsoft.EntityFrameworkCore;

namespace ChildAllowanceManager.Services;

public class ChildService(
    AllowanceDbContext db,
    IGlobalNotificationService globalNotificationService,
    ITransactionService transactionService,
    ILogger<ChildService> logger) : IChildService
{
    public async ValueTask<IEnumerable<ChildConfiguration>> GetChildren(string tenantId, CancellationToken cancellationToken = default) =>
        await db.Children.AsNoTracking()
            .Where(child => child.TenantId == tenantId && !child.Deleted)
            .OrderBy(child => child.FirstName)
            .ToListAsync(cancellationToken);

    public async ValueTask<IEnumerable<ChildWithBalanceHistory>> GetChildrenWithBalanceHistory(
        string tenantId, DateTimeOffset? startDate, DateTimeOffset? endDate, CancellationToken cancellationToken)
    {
        var children = await GetChildren(tenantId, cancellationToken);
        var result = new List<ChildWithBalanceHistory>();
        foreach (var child in children)
        {
            var history = await transactionService.GetBalanceHistoryForChild(
                child.Id, tenantId, startDate, endDate, cancellationToken);
            result.Add(new ChildWithBalanceHistory(child.Id, child.FirstName, child.TenantId, history.ToArray()));
        }

        return result;
    }

    public async ValueTask<IEnumerable<ChildWithBalance>> GetChildrenWithBalance(
        string tenantId, CancellationToken cancellationToken)
    {
        var children = await GetChildren(tenantId, cancellationToken);
        var result = new List<ChildWithBalance>();
        foreach (var child in children)
        {
            var latest = await transactionService.GetLatestTransactionForChild(child.Id, tenantId, cancellationToken);
            var latestRegular = await transactionService.GetLatestRegularTransactionForChild(child.Id, tenantId, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var balance = latest?.Balance ?? 0m;
            var lastRegularDate = latestRegular?.TransactionTimestamp.Date ?? now.Date.AddDays(-1);
            var baseNextDate = lastRegularDate >= now.Date ? now.AddDays(1) : now;
            var nextDate = new DateTimeOffset(baseNextDate.AddDays(child.HoldDaysRemaining).Date, TimeSpan.Zero);
            var isBirthday = child.BirthDate is not null && SameDayInYear(child.BirthDate, DateTime.Today);
            var nextIsBirthday = child.BirthDate is not null && SameDayInYear(nextDate.Date, child.BirthDate.Value.Date);

            result.Add(new ChildWithBalance
            {
                Id = child.Id,
                TenantId = child.TenantId,
                Balance = balance,
                Name = $"{child.FirstName} {child.LastName}",
                HoldDaysRemaining = child.HoldDaysRemaining,
                IsBirthday = isBirthday,
                NextRegularChange = nextIsBirthday && child.BirthdayAllowance is not null
                    ? child.BirthdayAllowance.Value
                    : child.RegularAllowance,
                NextRegularChangeDate = nextDate
            });
        }

        return result;
    }

    private static bool SameDayInYear(DateTime? first, DateTime? second) =>
        first is not null && second is not null &&
        first.Value.Month == second.Value.Month && first.Value.Day == second.Value.Day;

    public async ValueTask<ChildConfiguration> AddChild(ChildConfiguration child, CancellationToken cancellationToken = default)
    {
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
        var existing = await db.Children.FirstOrDefaultAsync(x => x.Id == child.Id, cancellationToken);
        if (existing is null)
        {
            existing = child;
            db.Children.Update(existing);
        }
        else
        {
            existing.FirstName = child.FirstName;
            existing.LastName = child.LastName;
            existing.BirthDate = child.BirthDate;
            existing.RegularAllowance = child.RegularAllowance;
            existing.HoldDaysRemaining = child.HoldDaysRemaining;
            existing.BirthdayAllowance = child.BirthdayAllowance;
            existing.TenantId = child.TenantId;
            existing.Deleted = child.Deleted;
        }

        existing.UpdatedTimestamp = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        globalNotificationService.OnChildStateChanged(existing.Id, existing.TenantId, string.Empty);
        return existing;
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

    public async ValueTask<ChildConfiguration?> GetChild(
        string childId, string childTenantId, CancellationToken cancellationToken = default) =>
        await db.Children.AsNoTracking().FirstOrDefaultAsync(
            child => child.Id == childId && child.TenantId == childTenantId && !child.Deleted,
            cancellationToken);
}
