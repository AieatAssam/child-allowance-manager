using ChildAllowanceManager.Common.Models;

namespace ChildAllowanceManager.Common.Interfaces;

/// The plaintext token of a newly minted link. Returned once and never recoverable.
public record CreatedShareLink(ShareLink Link, string Token);

public interface IShareLinkService
{
    ValueTask<CreatedShareLink> CreateAsync(
        string tenantId, string name, string createdByEmail,
        DateTimeOffset? expiresAt, CancellationToken ct = default);

    ValueTask<ShareLink?> ResolveAsync(string token, CancellationToken ct = default);

    ValueTask<IEnumerable<ShareLink>> GetForTenantAsync(string tenantId, CancellationToken ct = default);

    ValueTask<bool> RevokeAsync(string shareLinkId, string tenantId, CancellationToken ct = default);
}
