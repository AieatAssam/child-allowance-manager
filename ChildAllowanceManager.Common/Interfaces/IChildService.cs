using ChildAllowanceManager.Common.Models;

namespace ChildAllowanceManager.Common.Interfaces;

public interface IChildService
{
    public ValueTask<IEnumerable<ChildConfiguration>> GetChildren(string tenantId, CancellationToken cancellationToken);

    public ValueTask<IEnumerable<ChildWithBalance>> GetChildrenWithBalance(string tenantId, CancellationToken cancellationToken);
    
    public ValueTask<ChildConfiguration> AddChild(ChildConfiguration child, CancellationToken cancellationToken);
    
    public ValueTask<ChildConfiguration> UpdateChild(ChildConfiguration child, CancellationToken cancellationToken);
    
    public ValueTask<bool> DeleteChild(string id, string tenantId, CancellationToken cancellationToken);
    ValueTask<ChildConfiguration?> GetChild(string childId, string childTenantId, CancellationToken cancellationToken = default);

    ValueTask<IEnumerable<ChildWithBalanceHistory>> GetChildrenWithBalanceHistory(string tenantId, 
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        CancellationToken cancellationToken);
}