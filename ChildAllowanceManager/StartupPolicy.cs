using System.Linq;

namespace ChildAllowanceManager;

public static class StartupPolicy
{
    /// Returns true when the value is a usable setting rather than absent or an
    /// angle-bracket placeholder such as "&lt;ApplicationInsights-ConnectionString&gt;".
    public static bool IsConfigured(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !value.StartsWith('<');

    /// Builds the CSP frame-ancestors value. Always includes 'self'.
    /// Throws InvalidOperationException if any origin contains '*'.
    public static string BuildFrameAncestorsPolicy(IEnumerable<string> allowedOrigins)
    {
        var origins = allowedOrigins.ToArray();
        foreach (var origin in origins)
        {
            if (origin.Contains('*'))
                throw new InvalidOperationException($"FrameAncestors entry must not contain '*': {origin}");
        }

        return string.Join(' ', new[] { "'self'" }.Concat(origins));
    }
}
