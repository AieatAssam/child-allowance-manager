using ChildAllowanceManager.Common.Models;

namespace ChildAllowanceManager.Common.Interfaces;

public interface IUserService
{
    public Task InitializeUser(string email, string name, string? tenantId, CancellationToken cancellationToken);
    
    public Task UpsertUser(User user, CancellationToken cancellationToken);
    
    public ValueTask<User?> GetUserByEmail(string email, CancellationToken cancellationToken);
    
    public Task DeleteUser(string email, CancellationToken cancellationToken);
    
    public Task<IEnumerable<User>> GetUsers(CancellationToken cancellationToken);
}