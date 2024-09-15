using ChildAllowanceManager.Common.Models;

namespace ChildAllowanceManager.Common.Interfaces;

public interface IDataService
{
    public ValueTask<IEnumerable<ChildConfiguration>> GetChildren(string tenantId, CancellationToken cancellationToken);

    public ValueTask<IEnumerable<ChildWithBalance>> GetChildrenWithBalance(string tenantId, CancellationToken cancellationToken);
    
    public ValueTask<ChildConfiguration> AddChild(ChildConfiguration child, CancellationToken cancellationToken);
    
    public ValueTask<ChildConfiguration> UpdateChild(ChildConfiguration child, CancellationToken cancellationToken);
    
    public ValueTask<bool> DeleteChild(string id, string tenantId, CancellationToken cancellationToken);
    ValueTask<IEnumerable<TenantConfiguration>> GetTenants(CancellationToken cancellationToken = default);
    ValueTask<TenantConfiguration> AddTenant(TenantConfiguration tenant, CancellationToken cancellationToken = default);
    ValueTask<TenantConfiguration> UpdateTenant(TenantConfiguration tenant, CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteTenant(string id, CancellationToken cancellationToken = default);
    ValueTask<TenantConfiguration?> GetTenant(string id, CancellationToken cancellationToken = default);
    ValueTask<TenantConfiguration?> GetTenantBySuffix(string urlSuffix, CancellationToken cancellationToken = default);
    ValueTask<ChildConfiguration?> GetChild(string childId, string childTenantId, CancellationToken cancellationToken = default);

    ValueTask<IEnumerable<ChildWithBalanceHistory>> GetChildrenWithBalanceHistory(string tenantId, 
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        CancellationToken cancellationToken);
}