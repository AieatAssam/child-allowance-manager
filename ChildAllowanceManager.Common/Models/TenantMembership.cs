namespace ChildAllowanceManager.Common.Models;

/// A user's access to one family, with the role they hold there.
/// Replaces User.Tenants[] as the authority for tenant access.
public class TenantMembership : BaseItem
{
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;

    /// One of ValidRoles.Parent. Global roles (ValidRoles.Admin) stay on User.Roles.
    public string Role { get; set; } = ValidRoles.Parent;

    public User? User { get; set; }
    public TenantConfiguration? Tenant { get; set; }
}
