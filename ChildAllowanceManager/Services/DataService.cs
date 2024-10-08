using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.Azure.CosmosRepository;
using MudBlazor.Extensions;

namespace ChildAllowanceManager.Services;

public class DataService(HttpClient httpClient,
    IRepository<ChildConfiguration> childConfigurationRepository,
    IRepository<TenantConfiguration> tenantConfigurationRepository,
    IRepository<User> userRepository,
    ITransactionService transactionService,
    ILogger<DataService> logger) : IDataService
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

    public ValueTask<ChildConfiguration> UpdateChild(ChildConfiguration child, CancellationToken cancellationToken = default)
    {
        child.UpdatedTimestamp = DateTimeOffset.UtcNow;
        return childConfigurationRepository.UpdateAsync(child, cancellationToken:cancellationToken);
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
    
    public ValueTask<IEnumerable<TenantConfiguration>> GetTenants(CancellationToken cancellationToken = default)
    {
        return tenantConfigurationRepository.GetAsync((tenant) => !tenant.Deleted, cancellationToken);
    }
    
    public async ValueTask<TenantConfiguration?> GetTenant(string id, CancellationToken cancellationToken = default)
    {
        var result = await tenantConfigurationRepository.GetAsync(id, cancellationToken: cancellationToken);
        return result.Deleted ? null : result;
    }

    public async ValueTask<TenantConfiguration?> GetTenantBySuffix(string urlSuffix,
        CancellationToken cancellationToken = default)
    {
        var tenants = await tenantConfigurationRepository.GetAsync(
            (tenant) => tenant.UrlSuffix.Equals(urlSuffix, StringComparison.OrdinalIgnoreCase) &&
                        !tenant.Deleted, cancellationToken);
        return tenants.FirstOrDefault();
    }

    public async ValueTask<ChildConfiguration?> GetChild(string childId, string childTenantId, CancellationToken cancellationToken = default)
    {
        var result = await childConfigurationRepository.GetAsync(childId, childTenantId, cancellationToken: cancellationToken);
        return result.Deleted ? null : result;
    }

    public async ValueTask<TenantConfiguration> AddTenant(TenantConfiguration tenant, CancellationToken cancellationToken = default)
    {
        var existing = await GetTenantBySuffix(tenant.UrlSuffix, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Tenant with url suffix {tenant.UrlSuffix} already exists");
        }
        tenant.CreatedTimestamp = DateTimeOffset.UtcNow;
        tenant.UpdatedTimestamp = tenant.CreatedTimestamp;
        return await tenantConfigurationRepository.CreateAsync(tenant, cancellationToken);
    }
    
    public ValueTask<TenantConfiguration> UpdateTenant(TenantConfiguration tenant, CancellationToken cancellationToken = default)
    {
        tenant.UpdatedTimestamp = DateTimeOffset.UtcNow;
        return tenantConfigurationRepository.UpdateAsync(tenant, cancellationToken:cancellationToken);
    }
    
    public async ValueTask<bool> DeleteTenant(string id, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantConfigurationRepository.TryGetAsync(id, cancellationToken: cancellationToken);
        if (tenant is not null)
        {
            if (tenant.Deleted)
            {
                logger.LogWarning("Tenant {Id} is already deleted", id);
                return true;
            }

            tenant.Deleted = true;
            tenant.UpdatedTimestamp = DateTimeOffset.UtcNow;
            await tenantConfigurationRepository.UpdateAsync(tenant, cancellationToken: cancellationToken);
            
            // delete all children
            var children = await GetChildren(id, cancellationToken);
            foreach (var child in children)
            {
                await DeleteChild(child.Id, id, cancellationToken);
            }
            
            // remove tenant from all users
            var users = await userRepository.GetAsync((user) => user.Tenants.Contains(id), cancellationToken);
            foreach (var user in users)
            {
                user.Tenants = user.Tenants.Where(t => t != id).ToArray();
                await userRepository.UpdateAsync(user, cancellationToken: cancellationToken);
            }
            return true;
        }
        else
        {
            logger.LogWarning("Trying to delete tenant with id {Id} that does not exist", id);
            return false;
        }
    }
}