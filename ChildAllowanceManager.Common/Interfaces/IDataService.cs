using ChildAllowanceManager.Common.Models;

namespace ChildAllowanceManager.Common.Interfaces;

public interface IDataService
{
    public ValueTask<IEnumerable<ChildConfiguration>> GetChildren(Guid tenantId, CancellationToken cancellationToken);

    public ValueTask<ChildConfiguration> AddChild(ChildConfiguration child, CancellationToken cancellationToken);
    
    public ValueTask<ChildConfiguration> UpdateChild(ChildConfiguration child, CancellationToken cancellationToken);
    
    public ValueTask<bool> DeleteChild(string id, Guid tenantId, CancellationToken cancellationToken);
}