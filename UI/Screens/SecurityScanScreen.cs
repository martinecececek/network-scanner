using Spectre.Console;
using NetRadar.Core.Models;
using NetRadar.Core.Services;

namespace NetRadar.UI.Screens;

public enum SecurityScanAction { Back, OpenDetail, Quit }

public static class SecurityScanScreen
{
    // Phase 1 — port-scan all active devices and classify them
    public static async Task ScanAllAsync(
        List<NetworkDevice> devices,
        PortScanner portScanner,
        SecurityAnalyzer analyzer,
        DeviceTypeDetector typeDetector)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold red] Security Scan [/][grey50]— Scanning all active devices[/]")
            .RuleStyle("red").LeftJustified());
        AnsiConsole.WriteLine();

        var active = devices.Where(d => d.IsActive).ToList();
        if (active.Count == 0)
        {
            AnsiConsole.MarkupLine("  [yellow]No active devices to scan.[/]");
            Thread.Sleep(1500);
            return;
        }

        var sem  = new SemaphoreSlim(4);
        int done = 0;

        await AnsiConsole.Progress()
            .AutoRefresh(true)
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new ElapsedTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[red]Scanning for vulnerabilities[/]", maxValue: active.Count);

                await Task.WhenAll(active.Select(async dev =>
                {
                    await sem.WaitAsync();
                    try
                    {
                        dev.OpenPorts  = await portScanner.ScanAsync(dev.IpAddress);
                        dev.DeviceType = typeDetector.Classify(dev);
                        analyzer.Analyze(dev);
                    }
                    finally
                    {
                        task.Value = Interlocked.Increment(ref done);
                        sem.Release();
                    }
                }));
            });

        AnsiConsole.WriteLine();
        var high = active.Count(d => d.RiskLevel == RiskLevel.High);
        var med  = active.Count(d => d.RiskLevel == RiskLevel.Medium);

        AnsiConsole.MarkupLine(high > 0
            ? $"  [bold red]✖ {high} device(s) at HIGH risk![/]"
            : med > 0
                ? $"  [bold orange1]▲ {med} device(s) at MEDIUM risk.[/]"
                : "  [bold green]✓ No high-risk devices found.[/]");

        Thread.Sleep(1200);
    }

    // Phase 2 — interactive results screen (no re-scan)
    public static (SecurityScanAction action, NetworkDevice? selected) ShowResults(
        List<NetworkDevice> allDevices)
    {
        var scanned = allDevices
            .Where(d => d.IsActive && d.RiskLevel != RiskLevel.Unknown)
            .OrderByDescending(d => (int)d.RiskLevel)
            .ToList();

        int selectedIdx = 0;

        while (true)
        {
            Render(scanned, selectedIdx, allDevices.Count);
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    selectedIdx = Math.Max(0, selectedIdx - 1);
                    break;
                case ConsoleKey.DownArrow:
                    selectedIdx = Math.Min(scanned.Count - 1, selectedIdx + 1);
                    break;
                case ConsoleKey.Enter when scanned.Count > 0:
                    return (SecurityScanAction.OpenDetail, scanned[selectedIdx]);
                case ConsoleKey.B:
                case ConsoleKey.Escape:
                    return (SecurityScanAction.Back, null);
                case ConsoleKey.Q:
                    return (SecurityScanAction.Quit, null);
            }
        }
    }

    private static void Render(List<NetworkDevice> devices, int selectedIdx, int totalDevices)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold red] Security Scan Results [/]")
            .RuleStyle("red").LeftJustified());
        AnsiConsole.WriteLine();

        var high   = devices.Count(d => d.RiskLevel == RiskLevel.High);
        var medium = devices.Count(d => d.RiskLevel == RiskLevel.Medium);
        var low    = devices.Count(d => d.RiskLevel == RiskLevel.Low);
        var safe   = devices.Count(d => d.RiskLevel == RiskLevel.Safe);

        AnsiConsole.MarkupLine(
            $"  [bold red]✖ {high} HIGH[/]   [bold orange1]▲ {medium} MED[/]   [bold yellow]◆ {low} LOW[/]   [bold green]✓ {safe} SAFE[/]   [grey50]({totalDevices} total devices)[/]");
        AnsiConsole.WriteLine();

        if (devices.Count == 0)
        {
            AnsiConsole.MarkupLine("  [grey50]No active devices were scanned.[/]");
        }
        else
        {
            var maxRows  = Math.Max(3, Console.WindowHeight - 20);
            var startIdx = Math.Clamp(selectedIdx - maxRows / 2, 0, Math.Max(0, devices.Count - maxRows));
            var visible  = devices.Skip(startIdx).Take(maxRows).ToList();

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.BorderColor(Color.Red);
            table.Expand();

            table.AddColumn(new TableColumn("[bold grey50]  #[/]").RightAligned().Width(4));
            table.AddColumn(new TableColumn("[bold red]Risk[/]").Centered().Width(8));
            table.AddColumn(new TableColumn("[bold cyan]IP Address[/]").Width(16));
            table.AddColumn(new TableColumn("[bold cyan]Hostname[/]").Width(22));
            table.AddColumn(new TableColumn("[bold cyan]Type[/]").Width(16));
            table.AddColumn(new TableColumn("[bold cyan]Findings[/]"));

            for (int i = 0; i < visible.Count; i++)
            {
                var dev       = visible[i];
                var globalIdx = startIdx + i;
                var isSel     = globalIdx == selectedIdx;

                var rowNum   = $"{globalIdx + 1,3}";
                var ip       = dev.IpAddress;
                var hostname = dev.Hostname.Length > 20 ? dev.Hostname[..17] + "..." : dev.Hostname;
                var devType  = string.IsNullOrEmpty(dev.DeviceType) ? "Device" : dev.DeviceType;
                var findings = dev.SecurityFindings.Count > 0
                    ? string.Join(", ", dev.SecurityFindings.Take(2)) +
                      (dev.SecurityFindings.Count > 2 ? $" (+{dev.SecurityFindings.Count - 2})" : "")
                    : "No findings";

                var riskTag = FormatRiskTag(dev.RiskLevel);

                if (isSel)
                    table.AddRow(
                        $"[bold cyan on grey11] {rowNum} ►[/]",
                        $"[bold on grey11] {riskTag} [/]",
                        $"[bold white on grey11] {ip}[/]",
                        $"[bold white on grey11] {hostname}[/]",
                        $"[bold yellow on grey11] {devType}[/]",
                        $"[bold white on grey11] {findings}[/]"
                    );
                else
                    table.AddRow(
                        $"[grey50] {rowNum}  [/]",
                        $" {riskTag} ",
                        $"[white] {ip}[/]",
                        $"[white] {hostname}[/]",
                        $"[yellow] {devType}[/]",
                        $"[grey84] {findings}[/]"
                    );
            }

            AnsiConsole.Write(table);
        }

        if (devices.Count > 0)
            RenderFindingsPane(devices[selectedIdx]);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            "[grey50 on grey15]  [bold]↑↓[/]  Navigate    [bold]Enter[/]  Device Detail    [bold]Esc/B[/]  Back to table    [bold]Q[/]  Quit [/]");
    }

    private static string FormatRiskTag(RiskLevel level) => level switch
    {
        RiskLevel.High   => "[bold red]✖HIGH[/]",
        RiskLevel.Medium => "[bold orange1]▲MED[/]",
        RiskLevel.Low    => "[bold yellow]◆LOW[/]",
        RiskLevel.Safe   => "[bold green]✓SAFE[/]",
        _                => "[grey35]--[/]"
    };

    private static void RenderFindingsPane(NetworkDevice dev)
    {
        var (colorName, borderColor) = dev.RiskLevel switch
        {
            RiskLevel.High   => ("red",     Color.Red),
            RiskLevel.Medium => ("orange1", Color.Orange1),
            RiskLevel.Low    => ("yellow",  Color.Yellow),
            _                => ("green",   Color.Green)
        };

        var content = dev.SecurityFindings.Count > 0
            ? string.Join("\n", dev.SecurityFindings.Select(f => $"  [grey50]•[/] [white]{f}[/]"))
            : $"  [{colorName}]✓ No known vulnerabilities detected.[/]";

        var panel = new Panel(content)
        {
            Border  = BoxBorder.Rounded,
            Padding = new Padding(1, 0),
            Header  = new PanelHeader(
                $"[bold {colorName}] {dev.IpAddress} — {dev.Hostname} ({dev.DeviceType}) [/]")
        };
        panel.BorderColor(borderColor);
        AnsiConsole.Write(panel);
    }
}
