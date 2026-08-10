namespace ChildAllowanceManager.Common.Models;

public class TenantInvitation : BaseItem
{
    private string _email = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string Email
    {
        get => _email;
        set => _email = value.Trim().ToLowerInvariant();
    }

    public string Role { get; set; } = ValidRoles.Parent;
    public string InvitedByEmail { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public TenantConfiguration? Tenant { get; set; }
}
