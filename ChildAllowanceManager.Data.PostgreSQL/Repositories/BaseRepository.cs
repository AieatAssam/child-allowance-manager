using System.Linq.Expressions;
using ChildAllowanceManager.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChildAllowanceManager.Data.PostgreSQL.Repositories;

/// <summary>
/// Base repository implementation providing common functionality for all repositories
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public abstract class BaseRepository<T>(DbContext context) : IRepository<T>
    where T : class
{
    protected readonly DbContext _context = context;
    protected readonly DbSet<T> _dbSet = context.Set<T>();

    public virtual Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _dbSet.ToListAsync(cancellationToken);
    }

    public virtual ValueTask<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return _dbSet.FindAsync([id], cancellationToken);
    }

    public virtual Task<List<T>> GetAsync(Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default)
    {
        return _dbSet.Where(filter).ToListAsync(cancellationToken);
    }

    public virtual Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default)
    {
        return _dbSet.FirstOrDefaultAsync(filter, cancellationToken);
    }

    public virtual async ValueTask<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public virtual async ValueTask<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public virtual async ValueTask<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity == null)
            return false;

        _dbSet.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public virtual async ValueTask<bool> SoftDeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
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

    public virtual async ValueTask<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        Expression<Func<T, bool>>? filter = null,
        Expression<Func<T, object>>? orderBy = null,
        bool ascending = true,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (filter != null)
            query = query.Where(filter);

        if (orderBy != null)
            query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items.AsEnumerable(), totalCount);
    }

    public virtual Task<bool> ExistsAsync(Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default)
    {
        return _dbSet.AnyAsync(filter, cancellationToken);
    }

    public virtual Task<int> CountAsync(Expression<Func<T, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        return filter == null 
            ? _dbSet.CountAsync(cancellationToken)
            : _dbSet.CountAsync(filter, cancellationToken);
    }
} 