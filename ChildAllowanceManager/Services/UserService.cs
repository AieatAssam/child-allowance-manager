using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.Azure.CosmosRepository;

namespace ChildAllowanceManager.Services;

public class UserService(
    IRepository<User> userRepository,
    ILogger<UserService> logger
    ) : IUserService
{
    public async ValueTask<User> InitializeUserAsync(string email, string name, string? tenantId,
        CancellationToken cancellationToken)
    {
        var user = await GetUserByEmailAsync(email, cancellationToken) ?? new User();
        user.Name = name;
        user.Email = email;
        user.LastLoggedIn = DateTimeOffset.UtcNow;
        // add tenant to array if not already there
        if (!string.IsNullOrEmpty(tenantId))
        {
            user.Tenants = user.Tenants.Append(tenantId).Distinct().ToArray();
        }
        return await UpsertUserAsync(user, cancellationToken);
    }

    public async ValueTask<User> UpsertUserAsync(User user, CancellationToken cancellationToken)
    {
        var existingUser = await GetUserByEmailAsync(user.Email, cancellationToken);
        if (existingUser is not null)
        {
            user.UpdatedTimestamp = DateTimeOffset.UtcNow;
            user.CreatedTimestamp = existingUser.CreatedTimestamp;
            user.Id = existingUser.Id;
            return await userRepository.UpdateAsync(user, cancellationToken: cancellationToken);
        }
        else
        {
            user.CreatedTimestamp = DateTimeOffset.UtcNow;
            user.UpdatedTimestamp = user.CreatedTimestamp;
            return await userRepository.CreateAsync(user, cancellationToken: cancellationToken);
        }
    }

    public async ValueTask<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var users = await userRepository.GetAsync(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase),
            cancellationToken: cancellationToken);
        return users.SingleOrDefault(u => !u.Deleted);
    }

    public async Task DeleteUserAsync(string email, CancellationToken cancellationToken)
    {
        var user = await GetUserByEmailAsync(email, cancellationToken);
        if (user is not null)
        {
            user.Deleted = true;
            user.UpdatedTimestamp = DateTimeOffset.UtcNow;
            await userRepository.UpdateAsync(user, cancellationToken: cancellationToken);
        }
    }

    public async ValueTask<IEnumerable<User>> GetUsersAsync(CancellationToken cancellationToken)
    {
        return await userRepository.GetAsync(u => u.Deleted == false, cancellationToken: cancellationToken);
    }
    
    public async ValueTask<IEnumerable<User>> GetTenantUsersInRole(string tenantId, string role, CancellationToken cancellationToken)
    {
        var users = await userRepository.GetAsync(u => u.Tenants.Contains(tenantId) && u.Roles.Contains(role),
            cancellationToken: cancellationToken);
        return users;
    }
    
    public async ValueTask<bool> AddUserToTenantAsync(string email, string name, string tenantId, string role,
        CancellationToken cancellationToken)
    {
        var user = await GetUserByEmailAsync(email, cancellationToken);
        if (user is null)
        {
            user = await InitializeUserAsync(email, name, tenantId, cancellationToken);
        }

        user.Name = name;
        user.Tenants = user.Tenants.Append(tenantId).Distinct().ToArray();
        user.Roles = user.Roles.Append(role).Distinct().ToArray();
        await UpsertUserAsync(user, cancellationToken);
        return true;
    }
}