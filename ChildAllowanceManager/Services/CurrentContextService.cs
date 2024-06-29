using ChildAllowanceManager.Common.Interfaces;

namespace ChildAllowanceManager.Services;

public class CurrentContextService : ICurrentContextService
{
    private string? _currentTenant = null;
    public string? GetCurrentTenant()
    {
        return _currentTenant;
    }

    public void SetCurrentTenant(string tenantId)
    {
        _currentTenant = tenantId;
    }
}