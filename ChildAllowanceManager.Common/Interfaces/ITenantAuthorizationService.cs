using System.Security.Claims;

namespace ChildAllowanceManager.Common.Interfaces;

public interface ITenantAuthorizationService
{
    /// True when the principal may view this family.
    bool CanView(ClaimsPrincipal principal, string tenantId);

    /// True when the principal may change children, allowances and money in this family.
    bool CanManage(ClaimsPrincipal principal, string tenantId);

    /// True when the principal may invite or remove people in this family.
    bool CanManagePeople(ClaimsPrincipal principal, string tenantId);
}
