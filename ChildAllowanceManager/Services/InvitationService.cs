using System.ComponentModel.DataAnnotations;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Data;
using Microsoft.EntityFrameworkCore;

namespace ChildAllowanceManager.Services;

public class InvitationService(
    AllowanceDbContext db,
    UserService userService,
    MembershipService membershipService) : IInvitationService
{
    public const int InvitationDaysValid = 14;

    public async ValueTask<TenantInvitation> InviteAsync(
        string tenantId, string email, string role, CancellationToken ct = default)
    {
        email = email.Trim().ToLowerInvariant();
        if (!new EmailAddressAttribute().IsValid(email))
            throw new ValidationException("Enter a valid email address.");

        if ((await membershipService.GetMembershipsByEmailAsync(email, ct))
            .Any(x => x.TenantId == tenantId))
            throw new InvalidOperationException("That person already has access to this family.");

        var now = DateTimeOffset.UtcNow;
        var invitation = await db.TenantInvitations.FirstOrDefaultAsync(
            x => !x.Deleted && x.TenantId == tenantId && x.Email == email && x.AcceptedAt == null, ct);
        if (invitation is null)
        {
            invitation = new TenantInvitation
            {
                TenantId = tenantId,
                Email = email,
                Role = role,
                ExpiresAt = now.AddDays(InvitationDaysValid)
            };
            db.TenantInvitations.Add(invitation);
        }
        else
        {
            invitation.Role = role;
            invitation.ExpiresAt = now.AddDays(InvitationDaysValid);
            invitation.UpdatedTimestamp = now;
        }

        await db.SaveChangesAsync(ct);
        return invitation;
    }

    public async ValueTask<IEnumerable<TenantInvitation>> GetPendingForTenantAsync(
        string tenantId, CancellationToken ct = default) =>
        await db.TenantInvitations.AsNoTracking()
            .Where(x => !x.Deleted && x.TenantId == tenantId && x.AcceptedAt == null &&
                        x.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderBy(x => x.Email)
            .ToListAsync(ct);

    public async ValueTask<IEnumerable<TenantInvitation>> GetPendingForEmailAsync(
        string email, CancellationToken ct = default)
    {
        email = email.Trim().ToLowerInvariant();
        return await db.TenantInvitations.AsNoTracking()
            .Where(x => !x.Deleted && x.Email == email && x.AcceptedAt == null &&
                        x.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderBy(x => x.ExpiresAt)
            .ToListAsync(ct);
    }

    public async ValueTask<int> AcceptPendingAsync(
        string email, string name, CancellationToken ct = default)
    {
        email = email.Trim().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;
        var invitations = await db.TenantInvitations
            .Where(x => !x.Deleted && x.Email == email && x.AcceptedAt == null && x.ExpiresAt > now)
            .ToListAsync(ct);
        if (invitations.Count == 0)
            return 0;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await userService.InitializeUserAsync(email, name, null, db, ct);
        foreach (var invitation in invitations)
        {
            await membershipService.GrantAsync(user.Id, invitation.TenantId, invitation.Role, db, ct);
            invitation.AcceptedAt = now;
            invitation.UpdatedTimestamp = now;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return invitations.Count;
    }

    public async ValueTask<bool> RevokeAsync(
        string invitationId, string tenantId, CancellationToken ct = default)
    {
        var invitation = await db.TenantInvitations.FirstOrDefaultAsync(
            x => !x.Deleted && x.Id == invitationId && x.TenantId == tenantId && x.AcceptedAt == null, ct);
        if (invitation is null)
            return false;

        invitation.Deleted = true;
        invitation.UpdatedTimestamp = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }
}
