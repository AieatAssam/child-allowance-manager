using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.Azure.CosmosRepository;

namespace ChildAllowanceManager.Services;

public class DataService(HttpClient httpClient,
    IRepository<ChildConfiguration> childConfigurationRepository,
    ILogger<DataService> logger) : IDataService
{
    public ValueTask<IEnumerable<ChildConfiguration>> GetChildren(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return childConfigurationRepository.GetAsync((child) => child.TenantId == tenantId && !child.Deleted, cancellationToken);
    }

    public ValueTask<ChildConfiguration> AddChild(ChildConfiguration child, CancellationToken cancellationToken = default)
    {
        return childConfigurationRepository.CreateAsync(child, cancellationToken);
    }

    public ValueTask<ChildConfiguration> UpdateChild(ChildConfiguration child, CancellationToken cancellationToken = default)
    {
        return childConfigurationRepository.UpdateAsync(child, cancellationToken:cancellationToken);
    }

    public async ValueTask<bool> DeleteChild(string id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var child = await childConfigurationRepository.TryGetAsync(id, tenantId.ToString(), cancellationToken: cancellationToken);
        if (child is not null)
        {
            if (child.Deleted)
            {
                logger.LogWarning("Child {Id} is already deleted", id);
                return true;
            }

            child.Deleted = true;
            await childConfigurationRepository.UpdateAsync(child, cancellationToken: cancellationToken);
            return true;
        }
        else
        {
            logger.LogWarning("Trying to delete child with id {Id} that does not exist", id);
            return false;
        }
    }
}