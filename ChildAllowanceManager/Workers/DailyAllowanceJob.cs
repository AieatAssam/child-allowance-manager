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
    public async Task Execute(IJobExecutionContext context)
    {
        // create daily transactions for all children
        var tenants = await tenantService.GetTenants(context.CancellationToken);
        foreach (var tenant in tenants)
        {
            var children = await childService.GetChildrenWithBalance(tenant.Id, context.CancellationToken);
            foreach (var child in children)
            {
                var scheduledDate = (context.ScheduledFireTimeUtc ?? DateTimeOffset.UtcNow).UtcDateTime.Date;
                var latestRegular = await transactionService.GetLatestRegularTransactionForChild(
                    child.Id, child.TenantId, context.CancellationToken);
                if (child.HoldDaysRemaining > 0 ||
                    latestRegular?.TransactionTimestamp.UtcDateTime.Date >= scheduledDate)
                {
                    logger.LogWarning($"Skipping daily allowance for {child.Name} as it is held or already paid for {scheduledDate:yyyy-MM-dd}");
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
                logger.LogInformation($"Adding allowance transaction for {child.Name} with type {transaction.TransactionType}");
                await transactionService.AddTransaction(transaction, context.CancellationToken);

            }
            
            // process hold at the end of the tenant processing to ensure it is not cleared early
            await ProcessHoldForTenantAsync(tenant.Id, context.CancellationToken);
        }
    }

    private async Task ProcessHoldForTenantAsync(string tenantId, CancellationToken cancellationToken)
    {
        var children = await childService.GetChildren(tenantId, cancellationToken);
        foreach (var child in children.Where(child => child.HoldDaysRemaining > 0).ToList())
        {
            child.HoldDaysRemaining--;
            await childService.UpdateChild(child, cancellationToken);
        }
    }
}
