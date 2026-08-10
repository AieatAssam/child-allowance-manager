namespace ChildAllowanceManager.Common.Models;

public static class CustomClaimTypes
{
    public const string Tenant = "tenant";
    /// Value format "<tenantId>:<role>", one claim per live membership.
    public const string TenantRole = "tenant_role";
}

public static class TenantRoleClaim
{
    public static string Format(string tenantId, string role) => $"{tenantId}:{role}";

    public static bool TryParse(string value, out string tenantId, out string role)
    {
        var separator = value.IndexOf(':');
        if (separator <= 0 || separator == value.Length - 1)
        {
            tenantId = string.Empty;
            role = string.Empty;
            return false;
        }

        tenantId = value[..separator];
        role = value[(separator + 1)..];
        return true;
    }
}
