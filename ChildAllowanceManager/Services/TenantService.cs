using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Common.Validators;
using ChildAllowanceManager.Data;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ChildAllowanceManager.Services;

public class TenantService(
    AllowanceDbContext db,
    ILogger<TenantService> logger) : ITenantService
{
    private readonly TenantConfigurationValidator validator = new();

    public async ValueTask<IEnumerable<TenantConfiguration>> GetTenants(CancellationToken cancellationToken = default) =>
        await db.Tenants.AsNoTracking().Where(x => !x.Deleted).OrderBy(x => x.TenantName).ToListAsync(cancellationToken);

    public async ValueTask<IEnumerable<TenantConfiguration>> GetDeletedTenants(CancellationToken cancellationToken = default) =>
        await db.Tenants.AsNoTracking().Where(x => x.Deleted).OrderBy(x => x.TenantName).ToListAsync(cancellationToken);

    public async ValueTask<TenantConfiguration?> GetTenant(string id, CancellationToken cancellationToken = default) =>
        await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.Deleted, cancellationToken);

    public async ValueTask<TenantConfiguration?> GetTenantBySuffix(string urlSuffix, CancellationToken cancellationToken = default) =>
        await db.Tenants.AsNoTracking().FirstOrDefaultAsync(
            x => x.UrlSuffix == urlSuffix.Trim().ToLowerInvariant() && !x.Deleted, cancellationToken);

    public async ValueTask<TenantConfiguration> AddTenant(TenantConfiguration tenant, CancellationToken cancellationToken = default)
    {
        tenant.UrlSuffix = tenant.UrlSuffix.Trim().ToLowerInvariant();
        await ValidateAsync(tenant, cancellationToken);
        if (await db.Tenants.AnyAsync(x => x.UrlSuffix == tenant.UrlSuffix, cancellationToken))
            throw new InvalidOperationException($"Tenant with url suffix {tenant.UrlSuffix} already exists");
        tenant.CreatedTimestamp = DateTimeOffset.UtcNow;
        tenant.UpdatedTimestamp = tenant.CreatedTimestamp;
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);
        return tenant;
    }

    public async ValueTask<TenantConfiguration> UpdateTenant(TenantConfiguration tenant, CancellationToken cancellationToken = default)
    {
        tenant.UrlSuffix = tenant.UrlSuffix.Trim().ToLowerInvariant();
        await ValidateAsync(tenant, cancellationToken);
        if (await db.Tenants.AnyAsync(x => x.Id != tenant.Id && x.UrlSuffix == tenant.UrlSuffix, cancellationToken))
            throw new InvalidOperationException($"Tenant with url suffix {tenant.UrlSuffix} already exists");
        var existing = await db.Tenants.FirstOrDefaultAsync(x => x.Id == tenant.Id && !x.Deleted, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant {tenant.Id} was not found.");
        existing.TenantName = tenant.TenantName;
        existing.UrlSuffix = tenant.UrlSuffix;
        existing.UpdatedTimestamp = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async ValueTask<bool> DeleteTenant(string id, CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (tenant is null)
        {
            logger.LogWarning("Trying to delete tenant with id {Id} that does not exist", id);
            return false;
        }
        if (tenant.Deleted)
            return true;

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        tenant.Deleted = true;
        tenant.UpdatedTimestamp = now;

        var children = await db.Children
            .Where(x => x.TenantId == id && !x.Deleted)
            .ToListAsync(cancellationToken);
        foreach (var child in children)
        {
            child.Deleted = true;
            child.UpdatedTimestamp = now;
        }

        var memberships = await db.TenantMemberships
            .Where(x => x.TenantId == id && !x.Deleted)
            .ToListAsync(cancellationToken);
        foreach (var membership in memberships)
        {
            membership.Deleted = true;
            membership.UpdatedTimestamp = now;
        }

        var users = await db.Users.Where(x => !x.Deleted && x.Tenants.Contains(id)).ToListAsync(cancellationToken);
        foreach (var user in users)
        {
            user.Tenants = user.Tenants.Where(x => x != id).ToArray();
            user.UpdatedTimestamp = now;
        }
        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        return true;
    }

    public async ValueTask<bool> RestoreTenant(string id, CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(x => x.Id == id && x.Deleted, cancellationToken);
        if (tenant is null)
            return false;
        if (await db.Tenants.AnyAsync(
                x => x.Id != id && x.UrlSuffix == tenant.UrlSuffix && !x.Deleted, cancellationToken))
            throw new InvalidOperationException($"Tenant with url suffix {tenant.UrlSuffix} already exists");

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        tenant.Deleted = false;
        tenant.UpdatedTimestamp = now;

        var children = await db.Children.Where(x => x.TenantId == id && x.Deleted).ToListAsync(cancellationToken);
        foreach (var child in children)
        {
            child.Deleted = false;
            child.UpdatedTimestamp = now;
        }

        var memberships = await db.TenantMemberships
            .Where(x => x.TenantId == id && x.Deleted)
            .ToListAsync(cancellationToken);
        foreach (var membership in memberships)
        {
            membership.Deleted = false;
            membership.UpdatedTimestamp = now;
        }

        var userIds = memberships.Select(x => x.UserId).Distinct().ToArray();
        var users = await db.Users.Where(x => userIds.Contains(x.Id) && !x.Deleted).ToListAsync(cancellationToken);
        foreach (var user in users)
        {
            if (!user.Tenants.Contains(id))
                user.Tenants = user.Tenants.Append(id).ToArray();
            user.UpdatedTimestamp = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task ValidateAsync(TenantConfiguration tenant, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(tenant, cancellationToken);
        if (!result.IsValid)
            throw new ValidationException(result.Errors);
    }
}
