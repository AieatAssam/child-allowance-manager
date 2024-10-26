using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.Azure.CosmosRepository;
using MudBlazor.Extensions;

namespace ChildAllowanceManager.Services;

public class ChildService(HttpClient httpClient,
    IRepository<ChildConfiguration> childConfigurationRepository,
    IGlobalNotificationService globalNotificationService,
    ITransactionService transactionService,
    ILogger<ChildService> logger) : IChildService
{
    public ValueTask<IEnumerable<ChildConfiguration>> GetChildren(string tenantId, CancellationToken cancellationToken = default)
    {
        return childConfigurationRepository.GetAsync((child) => child.TenantId == tenantId && !child.Deleted, cancellationToken);
    }

    public async ValueTask<IEnumerable<ChildWithBalanceHistory>> GetChildrenWithBalanceHistory(string tenantId, 
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        CancellationToken cancellationToken)
    {
        var children = await GetChildren(tenantId, cancellationToken);
        var childrenWithBalanceHistory = new List<ChildWithBalanceHistory>();
        foreach (var child in children)
        {
            var balanceHistory = await transactionService.GetBalanceHistoryForChild(child.Id, tenantId, startDate, endDate, cancellationToken);
            childrenWithBalanceHistory.Add(new ChildWithBalanceHistory(child.Id, child.FirstName, child.TenantId, balanceHistory.ToArray()));
        }
        return childrenWithBalanceHistory;
    }
    
    public async ValueTask<IEnumerable<ChildWithBalance>> GetChildrenWithBalance(string tenantId, CancellationToken cancellationToken)
    {
        var children = await GetChildren(tenantId, cancellationToken);
        var childrenWithBalance = new List<ChildWithBalance>();
        foreach (var child in children)
        {
            // Retrieve the latest transaction for the child to have accurate balance
            var lastTransaction = await transactionService.GetLatestTransactionForChild(child.Id, tenantId, cancellationToken);
            // Retrieve the latest regular transaction for the child to have accurate next regular transaction stamp
            var lastRegularTransaction = await transactionService.GetLatestRegularTransactionForChild(child.Id, tenantId, cancellationToken);
            var balance = lastTransaction?.Balance ?? 0m;
            
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTimeOffset lastRegularTransactionDate = lastRegularTransaction?.TransactionTimestamp.Date ?? now.Date.AddDays(-1);

            // Calculate the base next transaction date
            DateTimeOffset baseNextTransactionDate = lastRegularTransactionDate.Date >= now.Date 
                ? now.AddDays(1) // If last transaction was today, next is tomorrow
                : now;           // If last transaction was before today, next is today

            // Add hold days to the base next transaction date
            DateTimeOffset nextRegularChangeDate = 
                new DateTimeOffset(
                    baseNextTransactionDate.AddDays(child.HoldDaysRemaining).Date, 
                    TimeSpan.Zero
                );

            var birthdayNext = child.BirthDate is not null &&
                               SameDayInYear(nextRegularChangeDate.Date, child.BirthDate.Value.Date);
            childrenWithBalance.Add(new ChildWithBalance
            {
                Id = child.Id,
                TenantId = child.TenantId,
                Balance = balance,
                Name = $"{child.FirstName} {child.LastName}",
                HoldDaysRemaining = child.HoldDaysRemaining,
                IsBirthday = child.BirthDate is not null &&
                             SameDayInYear(child.BirthDate, DateTime.Today),
                NextRegularChange = birthdayNext && child.BirthdayAllowance is not null
                    ? child.BirthdayAllowance.Value
                    : child.RegularAllowance,
                NextRegularChangeDate = nextRegularChangeDate,
            });
        }

        return childrenWithBalance;
    }
    
    private bool SameDayInYear(DateTime? first, DateTime? second) =>
        first is not null && second is not null &&
        first.Value.Month == second.Value.Month &&
        first.Value.Day == second.Value.Day;

    public async ValueTask<ChildConfiguration> AddChild(ChildConfiguration child, CancellationToken cancellationToken = default)
    {
        child.CreatedTimestamp = DateTimeOffset.UtcNow;
        child.UpdatedTimestamp = child.CreatedTimestamp;
        var result = await childConfigurationRepository.CreateAsync(child, cancellationToken);
        // initialise balance
        await transactionService.AddTransaction(new AllowanceTransaction
        {
            ChildId = result.Id,
            TenantId = result.TenantId,
            TransactionAmount = 0m,
            TransactionType = TransactionType.Adjustment,
            Description = "Initial balance"
        }, cancellationToken);
        return result;
    }

    public async ValueTask<ChildConfiguration> UpdateChild(ChildConfiguration child, CancellationToken cancellationToken = default)
    {
        child.UpdatedTimestamp = DateTimeOffset.UtcNow;
        var response = await childConfigurationRepository.UpdateAsync(child, cancellationToken:cancellationToken);
        
        // notify global notification service
        globalNotificationService.OnChildStateChanged(child.Id, 
            child.TenantId, 
            string.Empty);
        return response;
    }

    public async ValueTask<bool> DeleteChild(string id, string tenantId, CancellationToken cancellationToken = default)
    {
        var child = await childConfigurationRepository.TryGetAsync(id, tenantId, cancellationToken: cancellationToken);
        if (child is not null)
        {
            if (child.Deleted)
            {
                logger.LogWarning("Child {Id} is already deleted", id);
                return true;
            }

            child.Deleted = true;
            child.UpdatedTimestamp = DateTimeOffset.UtcNow;
            await childConfigurationRepository.UpdateAsync(child, cancellationToken: cancellationToken);
            return true;
        }
        else
        {
            logger.LogWarning("Trying to delete child with id {Id} that does not exist", id);
            return false;
        }
    }
    
    
    public async ValueTask<ChildConfiguration?> GetChild(string childId, string childTenantId, CancellationToken cancellationToken = default)
    {
        var result = await childConfigurationRepository.GetAsync(childId, childTenantId, cancellationToken: cancellationToken);
        return result.Deleted ? null : result;
    }
}