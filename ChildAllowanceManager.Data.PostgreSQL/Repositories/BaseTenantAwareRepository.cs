using System.Linq.Expressions;
using ChildAllowanceManager.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChildAllowanceManager.Data.PostgreSQL.Repositories;

/// <summary>
/// Base repository implementation for tenant-aware entities
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public abstract class BaseTenantAwareRepository<T>(DbContext context)
    : BaseRepository<T>(context), ITenantAwareRepository<T>
    where T : class
{
    public virtual Task<List<T>> GetAllForTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return _dbSet.Where(e => EF.Property<string>(e, "TenantId") == tenantId)
            .ToListAsync(cancellationToken);
    }

    public virtual Task<T?> GetByIdForTenantAsync(string id, string tenantId,
        CancellationToken cancellationToken = default)
    {
       return _dbSet.FirstOrDefaultAsync(e => 
            EF.Property<string>(e, "Id") == id && 
            EF.Property<string>(e, "TenantId") == tenantId, 
            cancellationToken);
    }

    public virtual Task<List<T>> GetForTenantAsync(string tenantId, Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default)
    {
        var tenantFilter = Expression.Lambda<Func<T, bool>>(
            Expression.AndAlso(
                Expression.Equal(
                    Expression.Property(Expression.Property(filter.Parameters[0], "TenantId"), "Value"),
                    Expression.Constant(tenantId)),
                filter.Body),
            filter.Parameters);

        return _dbSet.Where(tenantFilter).ToListAsync(cancellationToken);
    }

    public virtual Task<T?> GetFirstOrDefaultForTenantAsync(string tenantId, Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
    {
        var tenantFilter = Expression.Lambda<Func<T, bool>>(
            Expression.AndAlso(
                Expression.Equal(
                    Expression.Property(Expression.Property(filter.Parameters[0], "TenantId"), "Value"),
                    Expression.Constant(tenantId)),
                filter.Body),
            filter.Parameters);

        return _dbSet.FirstOrDefaultAsync(tenantFilter, cancellationToken);
    }

    public virtual async ValueTask<bool> DeleteForTenantAsync(string id, string tenantId, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdForTenantAsync(id, tenantId, cancellationToken);
        if (entity == null)
            return false;

        _dbSet.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public virtual async ValueTask<bool> SoftDeleteForTenantAsync(string id, string tenantId, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdForTenantAsync(id, tenantId, cancellationToken);
        if (entity == null)
            return false;

        var property = typeof(T).GetProperty("Deleted");
        if (property != null)
        {
            property.SetValue(entity, true);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        return false;
    }

    public virtual async ValueTask<(IEnumerable<T> Items, int TotalCount)> GetPagedForTenantAsync(
        string tenantId,
        int page,
        int pageSize,
        Expression<Func<T, bool>>? filter = null,
        Expression<Func<T, object>>? orderBy = null,
        bool ascending = true,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(e => EF.Property<string>(e, "TenantId") == tenantId);

        if (filter != null)
        {
            var tenantFilter = Expression.Lambda<Func<T, bool>>(
                Expression.AndAlso(
                    Expression.Equal(
                        Expression.Property(Expression.Property(filter.Parameters[0], "TenantId"), "Value"),
                        Expression.Constant(tenantId)),
                    filter.Body),
                filter.Parameters);
            query = query.Where(tenantFilter);
        }

        if (orderBy != null)
            query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items.AsEnumerable(), totalCount);
    }

    public virtual Task<bool> ExistsForTenantAsync(string tenantId, Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default)
    {
        var tenantFilter = Expression.Lambda<Func<T, bool>>(
            Expression.AndAlso(
                Expression.Equal(
                    Expression.Property(Expression.Property(filter.Parameters[0], "TenantId"), "Value"),
                    Expression.Constant(tenantId)),
                filter.Body),
            filter.Parameters);

        return _dbSet.AnyAsync(tenantFilter, cancellationToken);
    }

    public virtual Task<int> CountForTenantAsync(string tenantId, Expression<Func<T, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(e => EF.Property<string>(e, "TenantId") == tenantId);

        if (filter != null)
        {
            var tenantFilter = Expression.Lambda<Func<T, bool>>(
                Expression.AndAlso(
                    Expression.Equal(
                        Expression.Property(Expression.Property(filter.Parameters[0], "TenantId"), "Value"),
                        Expression.Constant(tenantId)),
                    filter.Body),
                filter.Parameters);
            query = query.Where(tenantFilter);
        }

        return query.CountAsync(cancellationToken);
    }
} 