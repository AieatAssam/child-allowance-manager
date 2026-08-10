using System.Security.Claims;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Authentication;

namespace ChildAllowanceManager.Services;

public class ClaimEnrichmentTransformer(IUserService _userService,
    ILogger<ClaimEnrichmentTransformer> _logger) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity) return principal;
        
        var email = identity.FindFirst(ClaimTypes.Email)?.Value;
        if (email is null)
        {
            _logger.LogWarning("No email claim found in the principal");
            return principal;
        }

        var matchingUser = await _userService.GetUserByEmailAsync(email, CancellationToken.None);
        if (matchingUser is null)
        {
            _logger.LogWarning("No user found for email {Email}. Cannot enrich claims", email);
            return principal;
        }

        foreach (var claim in identity.FindAll(CustomClaimTypes.Tenant)
                     .Where(c => !matchingUser.Tenants.Contains(c.Value))
                     .ToArray())
        {
            identity.RemoveClaim(claim);
        }

        foreach (var claim in identity.FindAll(ClaimTypes.Role)
                     .Where(c => (c.Value is ValidRoles.Admin or ValidRoles.Parent) &&
                                 !matchingUser.Roles.Contains(c.Value))
                     .ToArray())
        {
            identity.RemoveClaim(claim);
        }

        foreach (var role in matchingUser.Roles.Where(x =>
                     !identity.HasClaim(c => c.Type == ClaimTypes.Role && c.Value == x)))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        foreach (var tenantId in matchingUser.Tenants.Where(x =>
                     !identity.HasClaim(c => c.Type == CustomClaimTypes.Tenant && c.Value == x)))
        {
            identity.AddClaim(new Claim(CustomClaimTypes.Tenant, tenantId));
        }

        return principal;
    }
}
