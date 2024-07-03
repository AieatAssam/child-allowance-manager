using ChildAllowanceManager.Common.Models;

namespace ChildAllowanceManager.Common.Interfaces;

public interface IUserService
{
    public ValueTask<User> InitializeUserAsync(string email, string name, string? tenantId,
        CancellationToken cancellationToken);
    
    public ValueTask<User> UpsertUserAsync(User user, CancellationToken cancellationToken);
    
    public ValueTask<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken);
    
    public Task DeleteUserAsync(string email, CancellationToken cancellationToken);
    
    public ValueTask<IEnumerable<User>> GetUsersAsync(CancellationToken cancellationToken);
    ValueTask<IEnumerable<User>> GetTenantUsersInRole(string tenantId, string role, CancellationToken cancellationToken);
    ValueTask<bool> AddUserToTenantAsync(string email, string name, string tenantId, string role, CancellationToken cancellationToken);
}