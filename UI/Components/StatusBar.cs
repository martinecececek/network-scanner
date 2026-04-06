using Spectre.Console;

namespace NetRadar.UI.Components;

public static class StatusBar
{
    public static void RenderMain(bool filterActive, string sortMode, int shown, int total)
    {
        var filterTag = filterActive
            ? "[bold green on grey19] F:Active Only [/]"
            : "[grey50 on grey15] F:Filter [/]";

        var width = Console.WindowWidth;
        var left  = $" [grey50]↑↓[/][grey35] Navigate  [/][grey50]Enter[/][grey35] Details  [/][grey50]P[/][grey35] Ping  [/]{filterTag}[grey35]  [/][grey50]R[/][grey35] Rescan  [/][grey50]S[/][grey35] Security  [/][grey50]Tab[/][grey35] Sort:{sortMode}  [/][grey50]Q[/][grey35] Quit[/]";
        var right = $"[grey50]{shown}/{total} devices[/] ";

        AnsiConsole.WriteLine();
        AnsiConsole.Markup("[on grey15]" + new string(' ', width) + "[/]");
        Console.SetCursorPosition(0, Console.CursorTop - 1);
        AnsiConsole.Markup("[on grey15]" + left + "[/]");
        // Right-align device count
        try
        {
            var rightClean = $"{shown}/{total} devices ";
            var col = Math.Max(0, width - rightClean.Length - 1);
            Console.SetCursorPosition(col, Console.CursorTop);
            AnsiConsole.Markup($"[grey50 on grey15]{rightClean}[/]");
        }
        catch { /* ignore cursor positioning errors */ }
        AnsiConsole.WriteLine();
    }

    public static void RenderDetail()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey50 on grey15]  [bold]Esc[/] / [bold]B[/]  Back    [bold]R[/]  Rescan ports    [bold]Q[/]  Quit [/]");
    }
}
