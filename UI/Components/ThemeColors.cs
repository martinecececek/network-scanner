using Spectre.Console;

namespace NetRadar.UI.Components;

public static class ThemeColors
{
    // Status colors
    public const string Online  = "[bold green]";
    public const string Offline = "[grey50]";
    public const string Warning = "[bold yellow]";

    // Accent colors
    public const string Accent    = "[bold cyan]";
    public const string AccentDim = "[cyan]";
    public const string Title     = "[bold white]";
    public const string Subtle    = "[grey50]";
    public const string Muted     = "[grey35]";

    // Row highlight
    public const string SelectedRow = "bold cyan on grey11";
    public const string OfflineRow  = "grey50";

    // Status markup helpers
    public static Markup OnlineMarkup  => new("[bold green]ONLINE[/]");
    public static Markup OfflineMarkup => new("[grey50]OFFLINE[/]");

    public static string StatusMarkup(bool isActive) =>
        isActive ? "[bold green]● ONLINE[/]" : "[grey50]○ OFFLINE[/]";

    public static string PingMarkup(long pingMs)
    {
        if (pingMs < 0)  return "[grey50]  --[/]";
        if (pingMs < 10) return $"[bold green]{pingMs,3}ms[/]";
        if (pingMs < 50) return $"[green]{pingMs,3}ms[/]";
        if (pingMs < 150) return $"[yellow]{pingMs,3}ms[/]";
        return $"[red]{pingMs,3}ms[/]";
    }

    public static Color AppColor => Color.Cyan1;
}
