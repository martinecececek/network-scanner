using Spectre.Console;
using NetRadar.Core.Models;
using NetRadar.Core.Services;
using NetRadar.UI.Components;

namespace NetRadar.UI.Screens;

public enum DetailAction { Back, Rescan, Quit }

public static class DeviceDetailScreen
{
    public static async Task<DetailAction> ShowAsync(NetworkDevice device, PortScanner portScanner, DeviceTypeDetector typeDetector)
    {
        // Scan ports first (with spinner)
        await ScanPortsWithSpinner(device, portScanner, typeDetector);

        return ShowInteractive(device, portScanner, typeDetector);
    }

    private static async Task ScanPortsWithSpinner(NetworkDevice device, PortScanner portScanner, DeviceTypeDetector typeDetector)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold cyan] NetRadar [/][grey50]— Device Detail[/]").RuleStyle("cyan").LeftJustified());
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  [grey50]Scanning ports on[/] [bold white]{device.IpAddress}[/] [grey50]({device.Hostname})[/]");
        AnsiConsole.WriteLine();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync("[cyan]Scanning ports...[/]", async ctx =>
            {
                device.OpenPorts = await portScanner.ScanAsync(device.IpAddress);
                device.DeviceType = typeDetector.Classify(device);
            });
    }

    private static DetailAction ShowInteractive(NetworkDevice device, PortScanner portScanner, DeviceTypeDetector typeDetector)
    {
        while (true)
        {
            Render(device);
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                case ConsoleKey.B:
                    return DetailAction.Back;

                case ConsoleKey.R:
                    // Re-scan ports
                    ScanPortsWithSpinner(device, portScanner, typeDetector).GetAwaiter().GetResult();
                    break;

                case ConsoleKey.Q:
                    return DetailAction.Quit;
            }
        }
    }

    private static void Render(NetworkDevice device)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold cyan] NetRadar [/][grey50]— Device Detail[/]").RuleStyle("cyan").LeftJustified());
        AnsiConsole.WriteLine();

        var status = device.IsActive
            ? "[bold green]● ONLINE[/]"
            : "[bold red]○ OFFLINE[/]";
        var ping = device.PingMs >= 0 ? $"{device.PingMs}ms" : "--";
        var ttlStr = device.Ttl > 0 ? device.Ttl.ToString() : "--";
        var mac = string.IsNullOrEmpty(device.MacAddress) ? "--" : device.MacAddress;
        var vendor = string.IsNullOrEmpty(device.Vendor) ? "--" : device.Vendor;
        var deviceType = string.IsNullOrEmpty(device.DeviceType) ? "--" : device.DeviceType;

        // Info grid
        var infoGrid = new Grid();
        infoGrid.AddColumn(new GridColumn().NoWrap().Width(16));
        infoGrid.AddColumn(new GridColumn());

        infoGrid.AddRow("[bold cyan]IP Address[/]",   $"[bold white]{device.IpAddress}[/]");
        infoGrid.AddRow("[bold cyan]Hostname[/]",      $"[white]{device.Hostname}[/]");
        infoGrid.AddRow("[bold cyan]MAC Address[/]",   $"[white]{mac}[/]");
        infoGrid.AddRow("[bold cyan]Vendor[/]",         $"[white]{vendor}[/]");
        infoGrid.AddRow("[bold cyan]Device Type[/]",    $"[bold yellow]{deviceType}[/]");
        infoGrid.AddRow("[bold cyan]Status[/]",          status);
        infoGrid.AddRow("[bold cyan]Ping[/]",            $"[white]{ping}[/]  [grey50](TTL: {ttlStr})[/]");
        infoGrid.AddRow("[bold cyan]Last Seen[/]",       $"[grey50]{device.LastSeen:HH:mm:ss}[/]");

        AnsiConsole.Write(new Padder(infoGrid, new Padding(2, 0)));
        AnsiConsole.WriteLine();

        // Open ports section
        AnsiConsole.Write(new Rule("[bold cyan] Open Ports [/]").RuleStyle("cyan dim").LeftJustified());
        AnsiConsole.WriteLine();

        if (device.OpenPorts.Count == 0)
        {
            AnsiConsole.MarkupLine("  [grey50]No open ports detected (scanned common ports only)[/]");
        }
        else
        {
            var portGrid = new Grid();
            portGrid.AddColumn(new GridColumn().NoWrap().Width(8));
            portGrid.AddColumn(new GridColumn().NoWrap().Width(14));
            portGrid.AddColumn(new GridColumn());

            foreach (var port in device.OpenPorts)
            {
                var portName = PortScanner.GetPortName(port);
                var portColor = GetPortColor(port);
                portGrid.AddRow(
                    $"[{portColor}]● {port}[/]",
                    $"[{portColor}]{portName}[/]",
                    $"[grey50]{GetPortDescription(port)}[/]"
                );
            }
            AnsiConsole.Write(new Padder(portGrid, new Padding(2, 0)));
        }

        AnsiConsole.WriteLine();
        StatusBar.RenderDetail();
    }

    private static string GetPortColor(int port) => port switch
    {
        80 or 443 or 8080 => "green",
        22 => "cyan",
        3389 => "yellow",
        9100 => "magenta",
        _ => "white"
    };

    private static string GetPortDescription(int port) => port switch
    {
        21   => "File Transfer Protocol",
        22   => "Secure Shell",
        23   => "Telnet remote access",
        25   => "Email (SMTP)",
        53   => "Domain Name System",
        80   => "Web server (HTTP)",
        110  => "Email (POP3)",
        135  => "Windows RPC",
        139  => "Windows file sharing",
        443  => "Web server (HTTPS)",
        445  => "Windows SMB file sharing",
        3389 => "Remote Desktop Protocol",
        8080 => "Alternate HTTP / proxy",
        9100 => "Printer JetDirect",
        _    => ""
    };
}
