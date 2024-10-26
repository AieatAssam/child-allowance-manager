using ChildAllowanceManager.Common.Models;

namespace ChildAllowanceManager.Common.Interfaces;

public interface ITenantService
{
    ValueTask<IEnumerable<TenantConfiguration>> GetTenants(CancellationToken cancellationToken = default);
    ValueTask<TenantConfiguration?> GetTenant(string id, CancellationToken cancellationToken = default);

    ValueTask<TenantConfiguration?> GetTenantBySuffix(string urlSuffix,
        CancellationToken cancellationToken = default);

    ValueTask<TenantConfiguration> AddTenant(TenantConfiguration tenant, CancellationToken cancellationToken = default);
    ValueTask<TenantConfiguration> UpdateTenant(TenantConfiguration tenant, CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteTenant(string id, CancellationToken cancellationToken = default);
}