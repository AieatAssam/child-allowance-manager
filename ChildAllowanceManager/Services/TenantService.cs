using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.Azure.CosmosRepository;

namespace ChildAllowanceManager.Services;

public class TenantService(
    IRepository<TenantConfiguration> tenantConfigurationRepository,
    IRepository<User> userRepository,
    IChildService childService,
    ILogger<TenantService> logger) : ITenantService
{
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
            var children = await childService.GetChildren(id, cancellationToken);
            foreach (var child in children)
            {
                await childService.DeleteChild(child.Id, id, cancellationToken);
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