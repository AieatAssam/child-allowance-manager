using ChildAllowanceManager;
using Xunit;

namespace ChildAllowanceManager.Tests;

public class StartupConfigurationTests
{
    [Fact]
    public void IsConfigured_rejects_missing_and_placeholder_values()
    {
        Assert.False(StartupPolicy.IsConfigured(null));
        Assert.False(StartupPolicy.IsConfigured(string.Empty));
        Assert.False(StartupPolicy.IsConfigured("   "));
        Assert.False(StartupPolicy.IsConfigured("<ApplicationInsights-ConnectionString>"));
    }

    [Fact]
    public void IsConfigured_accepts_connection_strings()
    {
        Assert.True(StartupPolicy.IsConfigured("InstrumentationKey=abc;IngestionEndpoint=https://x/"));
    }

    [Fact]
    public void BuildFrameAncestorsPolicy_defaults_to_self()
    {
        Assert.Equal("'self'", StartupPolicy.BuildFrameAncestorsPolicy([]));
    }

    [Fact]
    public void BuildFrameAncestorsPolicy_includes_allowed_origins()
    {
        Assert.Equal(
            "'self' https://a.example https://b.example",
            StartupPolicy.BuildFrameAncestorsPolicy(["https://a.example", "https://b.example"]));
    }

    [Fact]
    public void BuildFrameAncestorsPolicy_rejects_wildcards()
    {
        Assert.Throws<InvalidOperationException>(() => StartupPolicy.BuildFrameAncestorsPolicy(["*"]));
    }

    [Fact]
    public void BuildFrameAncestorsPolicy_rejects_wildcard_origins()
    {
        Assert.Throws<InvalidOperationException>(
            () => StartupPolicy.BuildFrameAncestorsPolicy(["https://*.example"]));
    }
}
