using ChildAllowanceManager.Common.Interfaces;

namespace ChildAllowanceManager.Services;

public class CurrentContextService(IDataService _dataService) : ICurrentContextService
{
    private string? _currentTenant = null;
    public string? GetCurrentTenant()
    {
        return _currentTenant;
    }

    public async Task<string?> GetCurrentTenantSuffix()
    {
        if (!string.IsNullOrEmpty(_currentTenant))
        {
            var tenant = await _dataService.GetTenant(_currentTenant);
            return tenant?.UrlSuffix;
        }
        return null;
    }

    public void SetCurrentTenant(string tenantId)
    {
        _currentTenant = tenantId;
    }
}