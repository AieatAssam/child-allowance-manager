using System.Linq.Expressions;

namespace ChildAllowanceManager.Common.Interfaces;

/// <summary>
/// Generic repository interface for data access operations
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Gets all entities of type T
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A collection of entities</returns>
    Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets an entity by its ID
    /// </summary>
    /// <param name="id">The entity ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The entity if found, null otherwise</returns>
    ValueTask<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets entities based on a filter expression
    /// </summary>
    /// <param name="filter">The filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A collection of entities matching the filter</returns>
    Task<List<T>> GetAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = bad);

    /// <summary>
    /// Gets a single entity based on a filter expression
    /// </summary>
    /// <param name="filter">The filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The entity if found, null otherwise</returns>
    Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Adds a new entity
    /// </summary>
    /// <param name="entity">The entity to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The added entity with updated properties</returns>
    ValueTask<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates an existing entity
    /// </summary>
    /// <param name="entity">The entity to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated entity</returns>
    ValueTask<T> UpdateAsync(T entity, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes an entity by its ID
    /// </summary>
    /// <param name="id">The entity ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the entity was deleted, false otherwise</returns>
    ValueTask<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Soft deletes an entity by setting its Deleted property to true
    /// </summary>
    /// <param name="id">The entity ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the entity was soft deleted, false otherwise</returns>
    ValueTask<bool> SoftDeleteAsync(string id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a paged result of entities
    /// </summary>
    /// <param name="page">The page number (1-based)</param>
    /// <param name="pageSize">The page size</param>
    /// <param name="filter">Optional filter expression</param>
    /// <param name="orderBy">Optional ordering expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A paged result of entities</returns>
    ValueTask<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(
        int page, 
        int pageSize, 
        Expression<Func<T, bool>>? filter = null,
        Expression<Func<T, object>>? orderBy = null,
        bool ascending = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an entity exists based on a filter expression
    /// </summary>
    /// <param name="filter">The filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the entity exists, false otherwise</returns>
    Task<bool> ExistsAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts entities based on a filter expression
    /// </summary>
    /// <param name="filter">The filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The count of entities matching the filter</returns>
    Task<int> CountAsync(Expression<Func<T, bool>>? filter = null, CancellationToken cancellationToken = default);
} 