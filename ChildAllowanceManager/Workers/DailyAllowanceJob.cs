using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Services;
using Quartz;

namespace ChildAllowanceManager.Workers;

[DisallowConcurrentExecution]
public class DailyAllowanceJob(
    ITransactionService transactionService, 
    IChildService childService,
    ITenantService tenantService,
    ILogger<DailyAllowanceJob> logger) : IJob
{
    public ValueTask Execute(IJobExecutionContext context) => Execute(context, context.CancellationToken);

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        // create daily transactions for all children
        var tenants = await tenantService.GetTenants(cancellationToken);
        foreach (var tenant in tenants)
        {
            var fireTimeUtc = context.ScheduledFireTimeUtc ?? DateTimeOffset.UtcNow;
            var zone = ResolveZone(tenant.TimeZoneId);
            if (!string.IsNullOrWhiteSpace(tenant.TimeZoneId) &&
                !TimeZoneInfo.TryFindSystemTimeZoneById(tenant.TimeZoneId, out _))
            {
                logger.LogWarning("Unknown time zone {TimeZoneId} for tenant {TenantId}; falling back to UTC",
                    tenant.TimeZoneId, tenant.Id);
            }

            var tenantLocalNow = TimeZoneInfo.ConvertTime(fireTimeUtc, zone);
            // Pay only in the hour that starts the family's local day.
            if (tenantLocalNow.Hour != 0)
                continue;

            var scheduledDate = tenantLocalNow.Date;
            var children = await childService.GetChildrenWithBalance(tenant.Id, cancellationToken);
            foreach (var child in children)
            {
                var latestRegular = await transactionService.GetLatestRegularTransactionForChild(
                    child.Id, child.TenantId, cancellationToken);
                if (child.HoldDaysRemaining > 0 ||
                    (latestRegular?.AllowanceDate >= scheduledDate) == true)
                {
                    logger.LogWarning("Skipping daily allowance for {Child}; held or already paid for {Date:yyyy-MM-dd}",
                        child.Name, scheduledDate);
                    continue;
                }

                var transaction = new AllowanceTransaction
                {
                    ChildId = child.Id,
                    TenantId = child.TenantId,
                    TransactionAmount = child.NextRegularChange,
                    TransactionType = child.IsBirthday ? TransactionType.BirthdayAllowance : TransactionType.DailyAllowance,
                    Description = child.IsBirthday ? "Birthday allowance" : "Daily allowance",
                    AllowanceDate = scheduledDate
                };
                logger.LogInformation("Adding allowance transaction for {Child} with type {TransactionType}",
                    child.Name, transaction.TransactionType);
                await transactionService.AddTransaction(transaction, cancellationToken);

            }
            
            // process hold at the end of the tenant processing to ensure it is not cleared early
            await ProcessHoldForTenantAsync(tenant.Id, scheduledDate, cancellationToken);
        }
    }

    private async Task ProcessHoldForTenantAsync(string tenantId, DateTime scheduledDate,
        CancellationToken cancellationToken)
    {
        var children = await childService.GetChildren(tenantId, cancellationToken);
        foreach (var child in children.Where(child => child.HoldDaysRemaining > 0).ToList())
        {
            await childService.RemoveHoldDayAsync(
                child.Id,
                tenantId,
                $"hold-decrement:{child.Id}:{scheduledDate:yyyy-MM-dd}",
                cancellationToken);
        }
    }

    private static TimeZoneInfo ResolveZone(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id) &&
            TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
            return zone;
        return TimeZoneInfo.Utc;
    }
}
