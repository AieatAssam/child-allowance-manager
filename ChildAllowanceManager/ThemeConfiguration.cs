using MudBlazor;

namespace ChildAllowanceManager;

public class ThemeConfiguration
{
    public bool IsDarkMode { get; set; } = false;
    public MudTheme Theme { get; set; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#675184",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#5C5F6B",
            Tertiary = "#E9A36A",
            Background = "#FCFAF5",
            Surface = "#FFFFFF",
            TextPrimary = "#20242D",
            TextSecondary = "#5C5F6B",
            AppbarBackground = "#FCFAF5",
            AppbarText = "#20242D",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#20242D",
            LinesDefault = "#E4E0D8",
            Success = "#32735F",
            Warning = "#8A5A20",
            Error = "#A34F40",
            Info = "#54406E"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#B7A3D0",
            PrimaryContrastText = "#1B1A20",
            Secondary = "#A8A5B2",
            Tertiary = "#E9A36A",
            Background = "#1B1A20",
            Surface = "#24232B",
            TextPrimary = "#F2EFE9",
            TextSecondary = "#A8A5B2",
            AppbarBackground = "#1B1A20",
            AppbarText = "#F2EFE9",
            DrawerBackground = "#24232B",
            DrawerText = "#F2EFE9",
            LinesDefault = "#3A3844",
            Success = "#6FBFA2",
            Warning = "#E9A36A",
            Error = "#E08D7B",
            Info = "#CDBCE2"
        }
    },
    Typography = new Typography
    {
        Default = new DefaultTypography
        {
            FontFamily = new[] { "DM Sans", "system-ui", "sans-serif" }
        },
        H1 = new H1Typography
        {
            FontFamily = new[] { "Fraunces", "Georgia", "serif" }
        },
        H2 = new H2Typography
        {
            FontFamily = new[] { "Fraunces", "Georgia", "serif" }
        },
        H3 = new H3Typography
        {
            FontFamily = new[] { "Fraunces", "Georgia", "serif" }
        },
        H4 = new H4Typography
        {
            FontFamily = new[] { "Fraunces", "Georgia", "serif" }
        }
    };
}
