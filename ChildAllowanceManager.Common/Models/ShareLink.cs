namespace ChildAllowanceManager.Common.Models;

/// A revocable, login-free read-only grant over one family's balances and transactions.
/// The plaintext token is never persisted - only its SHA-256 hash. Revoking sets Deleted.
public class ShareLink : BaseItem
{
    public string TenantId { get; set; } = string.Empty;

    /// What this link is for, in the parent's words - "Kitchen tablet", "Nana's frame".
    /// Shown in the People page list so a parent can tell two links apart when revoking.
    public string Name { get; set; } = string.Empty;

    /// Lowercase hex SHA-256 of the plaintext token, 64 characters. The only copy of the
    /// secret that this system keeps. See share-token-plan.yaml S-D1.
    public string TokenHash { get; set; } = string.Empty;

    /// Email of the parent who minted it, for the People page list.
    public string CreatedByEmail { get; set; } = string.Empty;

    /// Null means the link never expires. See S-D4.
    public DateTimeOffset? ExpiresAt { get; set; }

    /// Last successful resolution, written at most hourly. See S-D10.
    public DateTimeOffset? LastAccessedAt { get; set; }

    public TenantConfiguration? Tenant { get; set; }
}
