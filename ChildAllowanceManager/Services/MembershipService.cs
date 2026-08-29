using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Data;
using Microsoft.EntityFrameworkCore;

namespace ChildAllowanceManager.Services;

public class MembershipService(AllowanceDbContext db) : IMembershipService
{
    public async ValueTask<IEnumerable<TenantMembership>> GetMembershipsForUserAsync(
        string userId, CancellationToken ct = default) =>
        await db.TenantMemberships.AsNoTracking()
            .Where(x => !x.Deleted && x.UserId == userId)
            .ToListAsync(ct);

    public async ValueTask<IEnumerable<TenantMembership>> GetMembershipsByEmailAsync(
        string email, CancellationToken ct = default)
    {
        email = email.Trim().ToLowerInvariant();
        return await (from membership in db.TenantMemberships.AsNoTracking()
                      join user in db.Users.AsNoTracking() on membership.UserId equals user.Id
                      where !membership.Deleted && !user.Deleted && user.Email == email
                      select membership).ToListAsync(ct);
    }

    public async ValueTask<IEnumerable<TenantMembership>> GetMembershipsForTenantAsync(
        string tenantId, CancellationToken ct = default) =>
        await db.TenantMemberships.AsNoTracking()
            .Include(x => x.User)
            .Where(x => !x.Deleted && x.TenantId == tenantId && x.User != null && !x.User.Deleted)
            .OrderBy(x => x.User!.Email)
            .ToListAsync(ct);

    public async ValueTask<TenantMembership> GrantAsync(
        string userId, string tenantId, string role, CancellationToken ct = default) =>
        await GrantAsync(userId, tenantId, role, db, ct);

    internal async ValueTask<TenantMembership> GrantAsync(
        string userId, string tenantId, string role, AllowanceDbContext context,
        CancellationToken ct = default)
    {
        var user = await context.Users.FirstOrDefaultAsync(x => x.Id == userId && !x.Deleted, ct)
            ?? throw new InvalidOperationException("User not found.");
        var membership = await context.TenantMemberships.FirstOrDefaultAsync(
            x => x.UserId == userId && x.TenantId == tenantId, ct);
        if (membership is null)
        {
            membership = new TenantMembership { UserId = userId, TenantId = tenantId, Role = role };
            context.TenantMemberships.Add(membership);
        }
        else
        {
            membership.Role = role;
            membership.Deleted = false;
            membership.UpdatedTimestamp = DateTimeOffset.UtcNow;
        }

        if (!user.Tenants.Contains(tenantId))
        {
            user.Tenants = user.Tenants.Append(tenantId).ToArray();
            user.UpdatedTimestamp = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(ct);
        return membership;
    }

    public async ValueTask<bool> RevokeAsync(
        string userId, string tenantId, CancellationToken ct = default)
    {
        var membership = await db.TenantMemberships.FirstOrDefaultAsync(
            x => !x.Deleted && x.UserId == userId && x.TenantId == tenantId, ct);
        if (membership is null)
            return false;

        if (membership.Role == ValidRoles.Parent &&
            await db.TenantMemberships.CountAsync(
                x => !x.Deleted && x.TenantId == tenantId && x.Role == ValidRoles.Parent, ct) == 1)
            throw new InvalidOperationException("A family must keep at least one parent.");

        membership.Deleted = true;
        membership.UpdatedTimestamp = DateTimeOffset.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId && !x.Deleted, ct);
        if (user is not null && user.Tenants.Contains(tenantId))
        {
            user.Tenants = user.Tenants.Where(x => x != tenantId).ToArray();
            user.UpdatedTimestamp = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async ValueTask<string?> GetRoleAsync(
        string userId, string tenantId, CancellationToken ct = default) =>
        await db.TenantMemberships.AsNoTracking()
            .Where(x => !x.Deleted && x.UserId == userId && x.TenantId == tenantId)
            .Select(x => x.Role)
            .FirstOrDefaultAsync(ct);
}
