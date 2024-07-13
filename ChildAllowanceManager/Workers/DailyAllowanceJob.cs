using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Services;
using Microsoft.AspNetCore.SignalR;
using Quartz;

namespace ChildAllowanceManager.Workers;

public class DailyAllowanceJob(
    ITransactionService transactionService, 
    IDataService dataService,
    ILogger<DailyAllowanceJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        // create daily transactions for all children
        var tenants = await dataService.GetTenants(context.CancellationToken);
        foreach (var tenant in tenants)
        {
            var children = await dataService.GetChildrenWithBalance(tenant.Id, context.CancellationToken);
            foreach (var child in children)
            {
                if (child.NextRegularChangeDate.UtcDateTime.Date > (context.ScheduledFireTimeUtc ?? DateTime.UtcNow).Date)
                {
                    // in the future, skip
                    logger.LogWarning($"Skipping daily allowance for {child.Name} as the next due date is {child.NextRegularChangeDate} and the current time is {context.ScheduledFireTimeUtc}");
                    continue;
                }

                var transaction = new AllowanceTransaction
                {
                    ChildId = child.Id,
                    TenantId = child.TenantId,
                    TransactionAmount = child.NextRegularChange,
                    TransactionType = child.IsBirthday ? TransactionType.BirthdayAllowance : TransactionType.DailyAllowance,
                    Description = child.IsBirthday ? "Birthday allowance" : "Daily allowance"
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
        var children = await dataService.GetChildren(tenantId, cancellationToken);
        foreach (var child in children.Where(child => child.HoldDaysRemaining > 0).ToList())
        {
            child.HoldDaysRemaining--;
            await dataService.UpdateChild(child, cancellationToken);
        }
    }
}