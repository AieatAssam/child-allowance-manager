using System.Linq.Expressions;

namespace ChildAllowanceManager.Common.Interfaces;

/// <summary>
/// Repository interface for tenant-aware entities
/// </summary>
/// <typeparam name="T">The entity type that is tenant-aware</typeparam>
public interface ITenantAwareRepository<T> : IRepository<T> where T : class
{
    /// <summary>
    /// Gets all entities for a specific tenant
    /// </summary>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A collection of entities for the specified tenant</returns>
    Task<List<T>> GetAllForTenantAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an entity by its ID and tenant ID
    /// </summary>
    /// <param name="id">The entity ID</param>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The entity if found, null otherwise</returns>
    Task<T?> GetByIdForTenantAsync(string id, string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets entities based on a filter expression for a specific tenant
    /// </summary>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="filter">The filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A collection of entities matching the filter for the specified tenant</returns>
    Task<List<T>> GetForTenantAsync(string tenantId, Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single entity based on a filter expression for a specific tenant
    /// </summary>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="filter">The filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The entity if found, null otherwise</returns>
    Task<T?> GetFirstOrDefaultForTenantAsync(string tenantId, Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = bad);
    
    /// <summary>
    /// Deletes an entity by its ID and tenant ID
    /// </summary>
    /// <param name="id">The entity ID</param>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the entity was deleted, false otherwise</returns>
    ValueTask<bool> DeleteForTenantAsync(string id, string tenantId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Soft deletes an entity by setting its Deleted property to true for a specific tenant
    /// </summary>
    /// <param name="id">The entity ID</param>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the entity was soft deleted, false otherwise</returns>
    ValueTask<bool> SoftDeleteForTenantAsync(string id, string tenantId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a paged result of entities for a specific tenant
    /// </summary>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="page">The page number (1-based)</param>
    /// <param name="pageSize">The page size</param>
    /// <param name="filter">Optional filter expression</param>
    /// <param name="orderBy">Optional ordering expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A paged result of entities for the specified tenant</returns>
    ValueTask<(IEnumerable<T> Items, int TotalCount)> GetPagedForTenantAsync(
        string tenantId,
        int page, 
        int pageSize, 
        Expression<Func<T, bool>>? filter = null,
        Expression<Func<T, object>>? orderBy = null,
        bool ascending = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an entity exists based on a filter expression for a specific tenant
    /// </summary>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="filter">The filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the entity exists, false otherwise</returns>
    Task<bool> ExistsForTenantAsync(string tenantId, Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts entities based on a filter expression for a specific tenant
    /// </summary>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="filter">Optional filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The count of entities matching the filter for the specified tenant</returns>
    Task<int> CountForTenantAsync(string tenantId, Expression<Func<T, bool>>? filter = null,
        CancellationToken cancellationToken = default);
} 