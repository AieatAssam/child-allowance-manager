using ChildAllowanceManager.Common.Models;

namespace ChildAllowanceManager.Common.Interfaces;

public interface IInvitationService
{
    ValueTask<TenantInvitation> InviteAsync(
        string tenantId, string email, string role, CancellationToken ct = default);

    ValueTask<IEnumerable<TenantInvitation>> GetPendingForTenantAsync(
        string tenantId, CancellationToken ct = default);

    ValueTask<IEnumerable<TenantInvitation>> GetPendingForEmailAsync(
        string email, CancellationToken ct = default);

    ValueTask<int> AcceptPendingAsync(
        string email, string name, CancellationToken ct = default);

    ValueTask<bool> RevokeAsync(
        string invitationId, string tenantId, CancellationToken ct = default);
}
