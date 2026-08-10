using System.Security.Claims;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;

namespace ChildAllowanceManager.Services;

public class TenantAuthorizationService : ITenantAuthorizationService
{
    public bool CanView(ClaimsPrincipal principal, string tenantId) =>
        principal.IsInRole(ValidRoles.Admin) || principal.HasClaim(CustomClaimTypes.Tenant, tenantId);

    public bool CanManage(ClaimsPrincipal principal, string tenantId) =>
        principal.IsInRole(ValidRoles.Admin) ||
        principal.HasClaim(CustomClaimTypes.TenantRole, TenantRoleClaim.Format(tenantId, ValidRoles.Parent));

    public bool CanManagePeople(ClaimsPrincipal principal, string tenantId) => CanManage(principal, tenantId);
}
