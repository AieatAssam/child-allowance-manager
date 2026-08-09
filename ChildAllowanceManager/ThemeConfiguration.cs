using MudBlazor;

namespace ChildAllowanceManager;

public class ThemeConfiguration
{
    public bool IsDarkMode { get; set; } = false;
    public MudTheme Theme { get; set; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#4F46E5",
            Secondary = "#F97316",
            Tertiary = "#0F766E",
            Background = "#F7F8FC",
            Surface = "#FFFFFF",
            TextPrimary = "#172033",
            TextSecondary = "#5B657A",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#172033",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#172033",
            Success = "#0F766E",
            Warning = "#D97706",
            Error = "#DC2626",
            Info = "#2563EB"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#A5B4FC",
            Secondary = "#FDBA74",
            Tertiary = "#5EEAD4",
            Background = "#111827",
            Surface = "#1F2937",
            TextPrimary = "#F9FAFB",
            TextSecondary = "#CBD5E1",
            AppbarBackground = "#1F2937",
            AppbarText = "#F9FAFB",
            DrawerBackground = "#1F2937",
            DrawerText = "#F9FAFB",
            Success = "#5EEAD4",
            Warning = "#FDBA74",
            Error = "#FCA5A5",
            Info = "#93C5FD"
        }
    };
}
