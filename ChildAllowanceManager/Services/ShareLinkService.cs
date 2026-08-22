using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Data;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace ChildAllowanceManager.Services;

public class ShareLinkService(AllowanceDbContext db, ILogger<ShareLinkService> logger)
    : IShareLinkService
{
    public async ValueTask<CreatedShareLink> CreateAsync(
        string tenantId, string name, string createdByEmail,
        DateTimeOffset? expiresAt, CancellationToken ct = default)
    {
        name = name.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 100)
            throw new ValidationException("Give this link a name so you can tell it apart later.");
        if (expiresAt is not null && expiresAt <= DateTimeOffset.UtcNow)
            throw new ValidationException("Choose an expiry date in the future.");

        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var now = DateTimeOffset.UtcNow;
        var link = new ShareLink
        {
            TenantId = tenantId,
            Name = name,
            TokenHash = HashToken(token),
            CreatedByEmail = createdByEmail,
            ExpiresAt = expiresAt,
            CreatedTimestamp = now,
            UpdatedTimestamp = now
        };
        db.ShareLinks.Add(link);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Share link {ShareLinkId} created for tenant {TenantId}", link.Id, tenantId);
        return new CreatedShareLink(link, token);
    }

    public async ValueTask<ShareLink?> ResolveAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var now = DateTimeOffset.UtcNow;
        var hash = HashToken(token);
        var link = await db.ShareLinks.Include(x => x.Tenant).FirstOrDefaultAsync(
            x => x.TokenHash == hash && !x.Deleted &&
                 (x.ExpiresAt == null || x.ExpiresAt > now), ct);
        if (link is null || link.Tenant is null || link.Tenant.Deleted)
            return null;

        if (link.LastAccessedAt is null || link.LastAccessedAt < now.AddHours(-1))
        {
            var touched = await db.ShareLinks
                .Where(x => x.Id == link.Id && !x.Deleted &&
                            (x.ExpiresAt == null || x.ExpiresAt > now) &&
                            (x.LastAccessedAt == null || x.LastAccessedAt < now.AddHours(-1)))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.LastAccessedAt, now)
                    .SetProperty(x => x.UpdatedTimestamp, now), ct);
            if (touched != 0)
            {
                link.LastAccessedAt = now;
                link.UpdatedTimestamp = now;
            }
        }

        return link;
    }

    public async ValueTask<IEnumerable<ShareLink>> GetForTenantAsync(
        string tenantId, CancellationToken ct = default) =>
        await db.ShareLinks.AsNoTracking()
            .Where(x => !x.Deleted && x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedTimestamp)
            .ToListAsync(ct);

    public async ValueTask<bool> RevokeAsync(
        string shareLinkId, string tenantId, CancellationToken ct = default)
    {
        var link = await db.ShareLinks.FirstOrDefaultAsync(
            x => !x.Deleted && x.Id == shareLinkId && x.TenantId == tenantId, ct);
        if (link is null)
            return false;

        link.Deleted = true;
        link.UpdatedTimestamp = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Share link {ShareLinkId} revoked for tenant {TenantId}", link.Id, tenantId);
        return true;
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
