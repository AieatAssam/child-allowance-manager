using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Quartz;

namespace ChildAllowanceManager.Workers;

public class DailyAllowanceJob(ITransactionService transactionService, IDataService dataService) : IJob
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
                if (Math.Abs((child.NextRegularChangeDate - (context.ScheduledFireTimeUtc ?? DateTime.UtcNow))
                        .TotalHours) > 1)
                {
                    // more than an hour off, not time for this child yet
                    continue;
                }

                var transaction = new AllowanceTransaction
                {
                    ChildId = child.Id,
                    TenantId = child.TenantId,
                    TransactionAmount = child.NextRegularChange,
                    TransactionType = child.IsBirthday ? TransactionType.BirthdayAllowance : TransactionType.DailyAllowance
                };
                await transactionService.AddTransaction(transaction, context.CancellationToken);
            }
        }
    }
}