using ChildAllowanceManager.Common.Models;

namespace ChildAllowanceManager.Common.Interfaces;

public interface IMembershipService
{
    /// Live memberships for a user, by their id.
    ValueTask<IEnumerable<TenantMembership>> GetMembershipsForUserAsync(string userId, CancellationToken ct = default);

    /// Live memberships for a user, by email. Returns empty when no such user exists.
    ValueTask<IEnumerable<TenantMembership>> GetMembershipsByEmailAsync(string email, CancellationToken ct = default);

    /// Live memberships within a family, ordered by the member's email.
    ValueTask<IEnumerable<TenantMembership>> GetMembershipsForTenantAsync(string tenantId, CancellationToken ct = default);

    /// Grants access, restoring a soft-deleted membership when present.
    ValueTask<TenantMembership> GrantAsync(string userId, string tenantId, string role, CancellationToken ct = default);

    /// Soft-deletes the membership. Returns false when there was nothing to revoke.
    ValueTask<bool> RevokeAsync(string userId, string tenantId, CancellationToken ct = default);

    /// The role this user holds in this family, or null when they hold none.
    ValueTask<string?> GetRoleAsync(string userId, string tenantId, CancellationToken ct = default);
}
