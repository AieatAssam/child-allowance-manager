using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Data;
using Microsoft.EntityFrameworkCore;

namespace ChildAllowanceManager.Services;

public class UserService(AllowanceDbContext db, MembershipService membershipService) : IUserService
{
    public async ValueTask<User> InitializeUserAsync(
        string email, string name, string? tenantId, CancellationToken cancellationToken) =>
        await InitializeUserAsync(email, name, tenantId, db, cancellationToken);

    internal async ValueTask<User> InitializeUserAsync(
        string email, string name, string? tenantId, AllowanceDbContext context,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(
                       x => !x.Deleted && x.Email == normalizedEmail, cancellationToken)
                   ?? new User { Email = normalizedEmail };
        user.Name = name;
        user.LastLoggedIn = DateTimeOffset.UtcNow;
        if (await context.Users.CountAsync(x => !x.Deleted, cancellationToken) == 0)
            user.Roles = [ValidRoles.Admin];
        if (!string.IsNullOrEmpty(tenantId))
            user.Tenants = user.Tenants.Append(tenantId).Distinct().ToArray();
        user = await UpsertUserAsync(user, context, cancellationToken);
        if (!string.IsNullOrEmpty(tenantId))
            await membershipService.GrantAsync(user.Id, tenantId, ValidRoles.Parent, context, cancellationToken);
        return user;
    }

    public async ValueTask<User> UpsertUserAsync(User user, CancellationToken cancellationToken) =>
        await UpsertUserAsync(user, db, cancellationToken);

    internal async ValueTask<User> UpsertUserAsync(
        User user, AllowanceDbContext context, CancellationToken cancellationToken)
    {
        user.Email = user.Email.Trim().ToLowerInvariant();
        var existing = await context.Users.FirstOrDefaultAsync(
            x => !x.Deleted && x.Email == user.Email, cancellationToken);
        if (existing is not null)
        {
            existing.Name = user.Name;
            existing.Roles = user.Roles;
            existing.Tenants = user.Tenants;
            existing.LastLoggedIn = user.LastLoggedIn;
            existing.UpdatedTimestamp = DateTimeOffset.UtcNow;
            user = existing;
        }
        else
        {
            user.CreatedTimestamp = DateTimeOffset.UtcNow;
            user.UpdatedTimestamp = user.CreatedTimestamp;
            context.Users.Add(user);
        }
        await context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async ValueTask<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking().FirstOrDefaultAsync(
            x => !x.Deleted && x.Email == email.Trim().ToLowerInvariant(), cancellationToken);

    public async Task DeleteUserAsync(string email, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(
            x => !x.Deleted && x.Email == email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null)
            return;
        user.Deleted = true;
        user.UpdatedTimestamp = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<IEnumerable<User>> GetUsersAsync(CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking().Where(x => !x.Deleted).OrderBy(x => x.Email).ToListAsync(cancellationToken);

    public async ValueTask<IEnumerable<User>> GetTenantUsersInRole(
        string tenantId, string role, CancellationToken cancellationToken) =>
        await (from user in db.Users.AsNoTracking()
               join membership in db.TenantMemberships.AsNoTracking() on user.Id equals membership.UserId
               where !user.Deleted && !membership.Deleted && membership.TenantId == tenantId && membership.Role == role
               orderby user.Email
               select user).ToListAsync(cancellationToken);

    public async ValueTask<bool> AddUserToTenantAsync(
        string email, string name, string tenantId, string role, CancellationToken cancellationToken)
    {
        var user = await GetUserByEmailAsync(email, cancellationToken)
                   ?? await InitializeUserAsync(email, name, null, cancellationToken);
        user.Name = name;
        await UpsertUserAsync(user, cancellationToken);
        await membershipService.GrantAsync(user.Id, tenantId, role, db, cancellationToken);
        return true;
    }
}
