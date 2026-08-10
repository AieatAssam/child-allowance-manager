namespace ChildAllowanceManager.Common.Interfaces;

public interface ICurrentContextService
{
    public string? GetCurrentTenant();
    
    public void SetCurrentTenant(string tenantId);
    ValueTask<string?> GetCurrentTenantSuffix();

    /// The signed-in user's email, lowercased, or null when there is no user.
    string? GetCurrentUserEmail();

    /// The signed-in user's display name, or "Allowance schedule" when there is no user.
    string GetCurrentUserName();
}
