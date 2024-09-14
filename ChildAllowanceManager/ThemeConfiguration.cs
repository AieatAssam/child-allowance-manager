using MudBlazor;

namespace ChildAllowanceManager;

public class ThemeConfiguration
{
    public bool IsDarkMode { get; set; } = false;
    public MudTheme Theme { get; set; } = new MudTheme();
}