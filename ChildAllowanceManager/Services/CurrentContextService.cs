using ChildAllowanceManager.Common.Interfaces;
using System.Security.Claims;

namespace ChildAllowanceManager.Services;

public class CurrentContextService(ITenantService tenantService, IHttpContextAccessor httpContextAccessor) : ICurrentContextService
{
    private string? _currentTenant = null;
    private readonly string? currentUserEmail = GetEmail(httpContextAccessor.HttpContext?.User);
    private readonly string currentUserName = GetName(httpContextAccessor.HttpContext?.User);

    private static string? GetEmail(ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true
            ? user.FindFirst(ClaimTypes.Email)?.Value.Trim().ToLowerInvariant()
            : null;

    private static string GetName(ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true &&
        !string.IsNullOrWhiteSpace(user.FindFirst(ClaimTypes.Name)?.Value)
            ? user.FindFirst(ClaimTypes.Name)!.Value
            : "Allowance schedule";
    public string? GetCurrentTenant()
    {
        return _currentTenant;
    }

    public async ValueTask<string?> GetCurrentTenantSuffix()
    {
        if (!string.IsNullOrEmpty(_currentTenant))
        {
            var tenant = await tenantService.GetTenant(_currentTenant);
            return tenant?.UrlSuffix;
        }
        return null;
    }

    public void SetCurrentTenant(string tenantId)
    {
        _currentTenant = tenantId;
    }

    public string? GetCurrentUserEmail() => currentUserEmail;
    public string GetCurrentUserName() => currentUserName;
}
