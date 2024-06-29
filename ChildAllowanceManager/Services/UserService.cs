using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.Azure.CosmosRepository;

namespace ChildAllowanceManager.Services;

public class UserService(
    IRepository<User> userRepository,
    ILogger<UserService> logger
    ) : IUserService
{
    public async Task InitializeUser(string email, string name, string? tenantId, CancellationToken cancellationToken)
    {
        var user = await GetUserByEmail(email, cancellationToken) ?? new User();
        user.Name = name;
        user.Email = email;
        // add tenant to array if not already there
        if (!string.IsNullOrEmpty(tenantId))
        {
            user.Tenants = user.Tenants.Append(tenantId).Distinct().ToArray();
        }
        await UpsertUser(user, cancellationToken);
    }

    public async Task UpsertUser(User user, CancellationToken cancellationToken)
    {
        var existingUser = await GetUserByEmail(user.Email, cancellationToken);
        if (existingUser is not null)
        {
            user.UpdatedTimestamp = DateTimeOffset.UtcNow;
            user.CreatedTimestamp = existingUser.CreatedTimestamp;
            user.Id = existingUser.Id;
            await userRepository.UpdateAsync(user, cancellationToken: cancellationToken);
        }
        else
        {
            user.CreatedTimestamp = DateTimeOffset.UtcNow;
            user.UpdatedTimestamp = user.CreatedTimestamp;
            await userRepository.CreateAsync(user, cancellationToken: cancellationToken);
        }
    }

    public async ValueTask<User?> GetUserByEmail(string email, CancellationToken cancellationToken)
    {
        if (await userRepository.ExistsAsync(email.ToLowerInvariant(), cancellationToken: cancellationToken) is false)
        {
            return null;
        }
        var users = await userRepository.GetAsync(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && u.Deleted == false, cancellationToken:cancellationToken);
        return users.SingleOrDefault(u => !u.Deleted);
    }

    public async Task DeleteUser(string email, CancellationToken cancellationToken)
    {
        var user = await GetUserByEmail(email, cancellationToken);
        if (user is not null)
        {
            user.Deleted = true;
            user.UpdatedTimestamp = DateTimeOffset.UtcNow;
            await userRepository.UpdateAsync(user, cancellationToken: cancellationToken);
        }
    }

    public async Task<IEnumerable<User>> GetUsers(CancellationToken cancellationToken)
    {
        return await userRepository.GetAsync(u => u.Deleted == false, cancellationToken: cancellationToken);
    }
}