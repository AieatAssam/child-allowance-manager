using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Data;
using Microsoft.EntityFrameworkCore;

namespace ChildAllowanceManager.Services;

public class UserService(AllowanceDbContext db) : IUserService
{
    public async ValueTask<User> InitializeUserAsync(
        string email, string name, string? tenantId, CancellationToken cancellationToken)
    {
        var user = await GetUserByEmailAsync(email, cancellationToken) ?? new User { Email = email };
        user.Name = name;
        user.LastLoggedIn = DateTimeOffset.UtcNow;
        if (await db.Users.CountAsync(x => !x.Deleted, cancellationToken) == 0)
            user.Roles = [ValidRoles.Admin];
        if (!string.IsNullOrEmpty(tenantId))
            user.Tenants = user.Tenants.Append(tenantId).Distinct().ToArray();
        return await UpsertUserAsync(user, cancellationToken);
    }

    public async ValueTask<User> UpsertUserAsync(User user, CancellationToken cancellationToken)
    {
        user.Email = user.Email.Trim().ToLowerInvariant();
        var existing = await db.Users.FirstOrDefaultAsync(
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
            db.Users.Add(user);
        }
        await db.SaveChangesAsync(cancellationToken);
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
        await db.Users.AsNoTracking()
            .Where(x => !x.Deleted && x.Tenants.Contains(tenantId) && x.Roles.Contains(role))
            .ToListAsync(cancellationToken);

    public async ValueTask<bool> AddUserToTenantAsync(
        string email, string name, string tenantId, string role, CancellationToken cancellationToken)
    {
        var user = await GetUserByEmailAsync(email, cancellationToken)
                   ?? await InitializeUserAsync(email, name, tenantId, cancellationToken);
        user.Name = name;
        user.Tenants = user.Tenants.Append(tenantId).Distinct().ToArray();
        user.Roles = user.Roles.Append(role).Distinct().ToArray();
        await UpsertUserAsync(user, cancellationToken);
        return true;
    }
}
