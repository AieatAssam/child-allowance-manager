namespace ChildAllowanceManager.Common.Interfaces;

public interface ICurrentContextService
{
    public string? GetCurrentTenant();
    
    public void SetCurrentTenant(string tenantId);
    Task<string?> GetCurrentTenantSuffix();
}