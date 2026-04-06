using Spectre.Console;
using NetRadar.Core.Services;

namespace NetRadar.UI.Screens;

public static class SplashScreen
{
    public static void Show(NetworkDiscovery discovery)
    {
        AnsiConsole.Clear();

        var figlet = new FigletText("NetRadar")
            .Color(Color.Cyan1)
            .Centered();
        AnsiConsole.Write(figlet);

        AnsiConsole.MarkupLine("[grey50]          [[ Network Intelligence Scanner v1.0 ]][/]");
        AnsiConsole.WriteLine();

        var (localIp, subnet) = discovery.DetectLocalNetwork();
        var allIfaces = discovery.GetAllInterfaces();

        // Main info
        var infoGrid = new Grid();
        infoGrid.AddColumn(new GridColumn().NoWrap().Width(16));
        infoGrid.AddColumn(new GridColumn());
        infoGrid.AddRow("[bold cyan]Selected IP[/]",    $"[bold white]{localIp}[/]");
        infoGrid.AddRow("[bold cyan]Scan Subnet[/]",    $"[bold white]{subnet}.0/24[/]");
        infoGrid.AddRow("[bold cyan]Scan Range[/]",     $"[white]{subnet}.1 – {subnet}.254[/]");

        var panel = new Panel(infoGrid)
        {
            Header  = new PanelHeader("[bold cyan] Selected Interface [/]"),
            Border  = BoxBorder.Rounded,
            Padding = new Padding(2, 1)
        };
        panel.BorderColor(Color.Cyan1);
        AnsiConsole.Write(new Padder(panel, new Padding(2, 0)));
        AnsiConsole.WriteLine();

        // Show ALL interfaces so the user can verify the right one is picked
        if (allIfaces.Count > 0)
        {
            AnsiConsole.MarkupLine("  [grey50]All detected network interfaces:[/]");
            var ifaceTable = new Table();
            ifaceTable.Border(TableBorder.Simple);
            ifaceTable.BorderColor(Color.Grey35);
            ifaceTable.AddColumn(new TableColumn("[grey50]Adapter[/]"));
            ifaceTable.AddColumn(new TableColumn("[grey50]IP[/]"));
            ifaceTable.AddColumn(new TableColumn("[grey50]Subnet[/]"));
            ifaceTable.AddColumn(new TableColumn("[grey50]Gateway[/]").Centered());
            ifaceTable.AddColumn(new TableColumn("[grey50]Virtual[/]").Centered());

            foreach (var (name, ip, sub, hasGw, isVirt) in allIfaces)
            {
                var isSelected = ip == localIp;
                var nameStr  = name.Length > 30 ? name[..27] + "..." : name;
                var gwStr    = hasGw  ? "[green]Yes[/]" : "[grey50]No[/]";
                var virtStr  = isVirt ? "[yellow]Yes[/]" : "[grey50]No[/]";
                var ipStr    = isSelected ? $"[bold cyan]{ip}[/] [cyan]◄[/]" : $"[grey84]{ip}[/]";
                var subStr   = isSelected ? $"[bold cyan]{sub}[/]" : $"[grey84]{sub}[/]";
                var nameMarkup = isSelected ? $"[bold white]{nameStr}[/]" : $"[grey50]{nameStr}[/]";

                ifaceTable.AddRow(nameMarkup, ipStr, subStr, gwStr, virtStr);
            }

            AnsiConsole.Write(new Padder(ifaceTable, new Padding(2, 0)));
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey50]  If the wrong interface is selected, virtual adapters (Hyper-V/VMware) may be interfering.[/]");
        AnsiConsole.MarkupLine("[grey50]  Press any key to begin scanning...[/]");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 4000)
        {
            if (Console.KeyAvailable) { Console.ReadKey(intercept: true); break; }
            Thread.Sleep(50);
        }
    }
}
