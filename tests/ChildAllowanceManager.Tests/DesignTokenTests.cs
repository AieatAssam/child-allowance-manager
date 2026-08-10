namespace ChildAllowanceManager.Tests;

public class DesignTokenTests
{
    [Fact]
    public void Token_stylesheet_contains_the_brand_palette()
    {
        var css = File.ReadAllText(Path.Combine("ChildAllowanceManager", "wwwroot", "tokens.css"));
        Assert.Contains("--color-ink", css);
        Assert.Contains("--color-paper", css);
        Assert.Contains("--color-accent", css);
    }

    [Fact]
    public void Token_stylesheet_defines_type_and_spacing_scales()
    {
        var css = File.ReadAllText(Path.Combine("ChildAllowanceManager", "wwwroot", "tokens.css"));
        Assert.Contains("--font-body", css);
        Assert.Contains("--space-4", css);
        Assert.Contains("--radius-card", css);
    }
}
