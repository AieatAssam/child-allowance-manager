namespace ChildAllowanceManager.Tests;

public class DesignTokenTests
{
    [Fact]
    public void Token_stylesheet_contains_the_brand_palette()
    {
        var css = ReadSource("ChildAllowanceManager/wwwroot/tokens.css");
        Assert.Contains("--al-ink", css);
        Assert.Contains("--al-paper", css);
        Assert.Contains("--al-accent", css);
    }

    [Fact]
    public void Token_stylesheet_defines_type_and_spacing_scales()
    {
        var css = ReadSource("ChildAllowanceManager/wwwroot/tokens.css");
        Assert.Contains("--al-font-ui", css);
        Assert.Contains("--al-space-4", css);
        Assert.Contains("--al-radius-lg", css);
    }

    private static string ReadSource(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "plan.yaml")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory!.FullName, relativePath));
    }
}
