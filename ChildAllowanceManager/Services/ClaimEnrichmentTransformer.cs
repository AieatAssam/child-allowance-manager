using System.Security.Claims;
using ChildAllowanceManager.Common.Interfaces;
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
            _logger.LogWarning("No user found for email {Email}. Cannot enrich cla with rolesims", email);
            return principal;
        }

        foreach (var role in matchingUser.Roles.Where(x =>
                     !identity.HasClaim(c => c.Type == ClaimTypes.Role && c.Value == x)))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        return principal;
    }
}