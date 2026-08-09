using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Data;
using Microsoft.EntityFrameworkCore;

namespace ChildAllowanceManager.Services;

public class TenantService(
    AllowanceDbContext db,
    IChildService childService,
    ILogger<TenantService> logger) : ITenantService
{
    public async ValueTask<IEnumerable<TenantConfiguration>> GetTenants(CancellationToken cancellationToken = default) =>
        await db.Tenants.AsNoTracking().Where(x => !x.Deleted).OrderBy(x => x.TenantName).ToListAsync(cancellationToken);

    public async ValueTask<TenantConfiguration?> GetTenant(string id, CancellationToken cancellationToken = default) =>
        await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.Deleted, cancellationToken);

    public async ValueTask<TenantConfiguration?> GetTenantBySuffix(string urlSuffix, CancellationToken cancellationToken = default) =>
        await db.Tenants.AsNoTracking().FirstOrDefaultAsync(
            x => x.UrlSuffix.ToLower() == urlSuffix.ToLower() && !x.Deleted, cancellationToken);

    public async ValueTask<TenantConfiguration> AddTenant(TenantConfiguration tenant, CancellationToken cancellationToken = default)
    {
        if (await GetTenantBySuffix(tenant.UrlSuffix, cancellationToken) is not null)
            throw new InvalidOperationException($"Tenant with url suffix {tenant.UrlSuffix} already exists");
        tenant.CreatedTimestamp = DateTimeOffset.UtcNow;
        tenant.UpdatedTimestamp = tenant.CreatedTimestamp;
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);
        return tenant;
    }

    public async ValueTask<TenantConfiguration> UpdateTenant(TenantConfiguration tenant, CancellationToken cancellationToken = default)
    {
        tenant.UpdatedTimestamp = DateTimeOffset.UtcNow;
        db.Tenants.Update(tenant);
        await db.SaveChangesAsync(cancellationToken);
        return tenant;
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

        tenant.Deleted = true;
        tenant.UpdatedTimestamp = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        foreach (var child in await childService.GetChildren(id, cancellationToken))
            await childService.DeleteChild(child.Id, id, cancellationToken);

        var users = await db.Users.Where(x => !x.Deleted && x.Tenants.Contains(id)).ToListAsync(cancellationToken);
        foreach (var user in users)
        {
            user.Tenants = user.Tenants.Where(x => x != id).ToArray();
            user.UpdatedTimestamp = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
