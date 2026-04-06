using Spectre.Console;
using NetRadar.Core.Models;
using NetRadar.Core.Services;
using NetRadar.UI.Components;

namespace NetRadar.UI.Screens;

public enum SortMode { ByIp, ByPing, ByHostname }
public enum NavAction { None, OpenDetail, Rescan, SecurityScan, Quit }

public class MainTableScreen
{
    private int _selectedIndex;
    private bool _showActiveOnly;
    private SortMode _sortMode = SortMode.ByIp;
    private List<NetworkDevice> _displayed = new();
    private string _statusMessage = "";
    private readonly DeviceTypeDetector _typeDetector;

    public MainTableScreen(DeviceTypeDetector typeDetector)
    {
        _typeDetector = typeDetector;
    }

    public async Task<(NavAction action, NetworkDevice? selected)> RunAsync(
        List<NetworkDevice> devices,
        NetworkDiscovery discovery)
    {
        _selectedIndex = Math.Min(_selectedIndex, Math.Max(0, GetDisplayed(devices).Count - 1));

        while (true)
        {
            _displayed = GetDisplayed(devices);

            if (_displayed.Count > 0)
                _selectedIndex = Math.Clamp(_selectedIndex, 0, _displayed.Count - 1);

            Render(devices);

            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    _selectedIndex = Math.Max(0, _selectedIndex - 1);
                    _statusMessage = "";
                    break;

                case ConsoleKey.DownArrow:
                    _selectedIndex = Math.Min(_displayed.Count - 1, _selectedIndex + 1);
                    _statusMessage = "";
                    break;

                case ConsoleKey.Enter when _displayed.Count > 0:
                    return (NavAction.OpenDetail, _displayed[_selectedIndex]);

                case ConsoleKey.P when _displayed.Count > 0:
                    await PingSelectedAsync(_displayed[_selectedIndex], discovery);
                    break;

                case ConsoleKey.F when key.Modifiers == 0:
                    _showActiveOnly = !_showActiveOnly;
                    _selectedIndex = 0;
                    _statusMessage = "";
                    break;

                case ConsoleKey.Tab:
                    _sortMode = (SortMode)(((int)_sortMode + 1) % 3);
                    _statusMessage = "";
                    break;

                case ConsoleKey.R:
                case ConsoleKey.F5:
                    return (NavAction.Rescan, null);

                case ConsoleKey.S:
                    return (NavAction.SecurityScan, null);

                case ConsoleKey.Q:
                case ConsoleKey.Escape:
                    return (NavAction.Quit, null);
            }
        }
    }

    public void ResetSelection()
    {
        _selectedIndex = 0;
        _statusMessage = "";
    }

    private async Task PingSelectedAsync(NetworkDevice device, NetworkDiscovery discovery)
    {
        _statusMessage = $"[cyan]Pinging {device.IpAddress}...[/]";
        Render(GetDisplayed(_displayed).Count > 0
            ? _displayed.Select(d => d).ToList()
            : new List<NetworkDevice>());

        var (isActive, pingMs, ttl) = await discovery.PingOneAsync(device.IpAddress);

        device.IsActive = isActive;
        device.PingMs = pingMs;
        if (ttl > 0) device.Ttl = ttl;
        device.LastSeen = DateTime.Now;

        _statusMessage = isActive
            ? pingMs >= 0
                ? $"[bold green]Ping {device.IpAddress}: {pingMs}ms[/]"
                : $"[bold yellow]Ping {device.IpAddress}: Active (ICMP blocked)[/]"
            : $"[bold red]Ping {device.IpAddress}: No response[/]";
    }

    private List<NetworkDevice> GetDisplayed(List<NetworkDevice> devices)
    {
        IEnumerable<NetworkDevice> list = _showActiveOnly
            ? devices.Where(d => d.IsActive)
            : devices;

        return _sortMode switch
        {
            SortMode.ByPing     => list.OrderBy(d => d.IsActive ? (d.PingMs >= 0 ? d.PingMs : 9998) : 9999).ToList(),
            SortMode.ByHostname => list.OrderBy(d => d.Hostname).ToList(),
            _                   => list.OrderBy(d => IpToLong(d.IpAddress)).ToList()
        };
    }

    private static long IpToLong(string ip)
    {
        try { return ip.Split('.').Select(long.Parse).Aggregate((a, b) => a * 256 + b); }
        catch { return 0; }
    }

    private void Render(List<NetworkDevice> allDevices)
    {
        AnsiConsole.Clear();

        // Header
        AnsiConsole.Write(new Rule("[bold cyan] NetRadar [/][grey50]— Network Scanner[/]")
            .RuleStyle("cyan")
            .LeftJustified());
        AnsiConsole.WriteLine();

        if (_displayed.Count == 0)
        {
            var msg = _showActiveOnly
                ? "[yellow]No active devices found. Press [bold]F[/] to show all.[/]"
                : "[yellow]No devices found. Press [bold]R[/] to rescan.[/]";
            AnsiConsole.MarkupLine($"  {msg}");
        }
        else
        {
            // Reserve 9 lines for the detail pane + status bar
            var maxRows = Math.Max(3, Console.WindowHeight - 23);
            var startIdx = Math.Max(0, _selectedIndex - maxRows / 2);
            startIdx = Math.Min(startIdx, Math.Max(0, _displayed.Count - maxRows));
            var visibleDevices = _displayed.Skip(startIdx).Take(maxRows).ToList();

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.BorderColor(Color.Grey);
            table.Expand();

            table.AddColumn(new TableColumn("[bold grey50]  #[/]").RightAligned().Width(4));
            table.AddColumn(new TableColumn("[bold cyan]IP Address[/]").Width(16));
            table.AddColumn(new TableColumn("[bold cyan]Hostname[/]"));
            table.AddColumn(new TableColumn("[bold cyan]Status[/]").Centered().Width(12));
            table.AddColumn(new TableColumn("[bold red]Risk[/]").Centered().Width(8));
            table.AddColumn(new TableColumn("[bold cyan]Ping[/]").RightAligned().Width(10));
            table.AddColumn(new TableColumn("[bold cyan]MAC Address[/]").Width(19));

            for (var i = 0; i < visibleDevices.Count; i++)
            {
                var dev = visibleDevices[i];
                var globalIdx = startIdx + i;
                var isSelected = globalIdx == _selectedIndex;

                var rowNum   = $"{globalIdx + 1,3}";
                var ip       = dev.IpAddress;
                var hostname = dev.Hostname.Length > 26 ? dev.Hostname[..23] + "..." : dev.Hostname;
                var mac      = string.IsNullOrEmpty(dev.MacAddress) ? "--" : dev.MacAddress;

                var (statusText, pingText) = FormatStatusAndPing(dev);
                var riskCell = FormatRiskCell(dev, isSelected);

                if (isSelected)
                {
                    table.AddRow(
                        $"[bold cyan on grey11] {rowNum} ►[/]",
                        $"[bold white on grey11] {ip}[/]",
                        $"[bold white on grey11] {hostname}[/]",
                        $"[bold on grey11] {statusText} [/]",
                        riskCell,
                        $"[bold white on grey11] {pingText} [/]",
                        $"[bold white on grey11] {mac}[/]"
                    );
                }
                else if (!dev.IsActive)
                {
                    table.AddRow(
                        $"[grey35] {rowNum}  [/]",
                        $"[grey50] {ip}[/]",
                        $"[grey50] {hostname}[/]",
                        $"[grey35] {statusText} [/]",
                        riskCell,
                        $"[grey35] {pingText} [/]",
                        $"[grey35] {mac}[/]"
                    );
                }
                else
                {
                    var pingColor = dev.PingMs < 0 ? "yellow"
                        : dev.PingMs < 10 ? "green"
                        : dev.PingMs < 50 ? "yellow"
                        : "red";
                    table.AddRow(
                        $"[grey50] {rowNum}  [/]",
                        $"[white] {ip}[/]",
                        $"[white] {hostname}[/]",
                        $"[bold green] {statusText} [/]",
                        riskCell,
                        $"[{pingColor}] {pingText} [/]",
                        $"[grey84] {mac}[/]"
                    );
                }
            }

            AnsiConsole.Write(table);

            if (_displayed.Count > maxRows)
            {
                var page  = (startIdx / maxRows) + 1;
                var pages = (_displayed.Count + maxRows - 1) / maxRows;
                AnsiConsole.MarkupLine($"  [grey50]Page {page}/{pages}  ({_displayed.Count} total)[/]");
            }
        }

        // ── Detail pane for selected device ──────────────────────────────────
        var selected = _displayed.Count > 0 ? _displayed[_selectedIndex] : null;
        RenderDetailPane(selected);

        // Status message (ping result or empty)
        if (!string.IsNullOrEmpty(_statusMessage))
            AnsiConsole.MarkupLine($"  {_statusMessage}");
        else
            AnsiConsole.WriteLine();

        var sortLabel = _sortMode switch
        {
            SortMode.ByPing     => "Ping",
            SortMode.ByHostname => "Hostname",
            _                   => "IP"
        };
        var filterLabel = _showActiveOnly ? "[bold green]Active Only[/]" : "[grey50]All[/]";
        AnsiConsole.MarkupLine($"  [grey50]Sort:[/] {sortLabel}  [grey50]Filter:[/] {filterLabel}  [grey50]Devices:[/] [white]{allDevices.Count(d => d.IsActive)}/{allDevices.Count}[/]");

        StatusBar.RenderMain(_showActiveOnly, sortLabel, _displayed.Count, allDevices.Count);
    }

    private static string FormatRiskCell(NetworkDevice dev, bool isSelected)
    {
        if (!dev.IsActive)
            return isSelected ? "[grey35 on grey11]--[/]" : "[grey35]--[/]";

        var bg = isSelected ? " on grey11" : "";
        return dev.RiskLevel switch
        {
            RiskLevel.High   => $"[bold red{bg}]HIGH[/]",
            RiskLevel.Medium => $"[bold orange1{bg}]MED[/]",
            RiskLevel.Low    => $"[bold yellow{bg}]LOW[/]",
            RiskLevel.Safe   => $"[bold green{bg}]SAFE[/]",
            _                => $"[grey50{bg}]--[/]"   // not yet scanned
        };
    }

    private static (string status, string ping) FormatStatusAndPing(NetworkDevice dev)
    {
        if (!dev.IsActive)
            return ("○ OFFLINE", "--");

        var pingText = dev.PingMs >= 0 ? $"{dev.PingMs}ms" : "FW BLOCK";
        return ("● ONLINE", pingText);
    }

    // Renders e.g.  ▐███░░░░░▌ 8ms   (8 blocks, scale: 1 block per 10ms, cap 80ms)
    private static string FormatLatencyBar(NetworkDevice dev)
    {
        const int bars    = 8;
        const int msPerBar = 10;   // each block = 10ms

        if (!dev.IsActive)
            return $"[grey35]▐{"░".PadRight(bars, '░')}▌ --[/]";

        // Firewall-blocked (alive but no ICMP)
        if (dev.PingMs < 0)
            return $"[yellow]▐{"?".PadLeft(bars / 2 + 1).PadRight(bars, '?')}▌ FW[/]";

        var filled   = Math.Min(bars, (int)Math.Ceiling(dev.PingMs / (double)msPerBar));
        filled       = Math.Max(1, filled); // always show at least 1 block if alive
        var empty    = bars - filled;
        var bar      = new string('█', filled) + new string('░', empty);
        var pingLabel = $"{dev.PingMs}ms";

        var color = dev.PingMs < 10 ? "green"
                  : dev.PingMs < 50 ? "yellow"
                  : "red";

        return $"[{color}]▐{bar}▌ {pingLabel,5}[/]";
    }

    private void RenderDetailPane(NetworkDevice? dev)
    {
        if (dev == null)
        {
            AnsiConsole.Write(new Panel("[grey50]No device selected[/]")
            {
                Border  = BoxBorder.Rounded,
                Padding = new Padding(1, 0),
                Header  = new PanelHeader("[grey50] Device Details [/]")
            }.BorderColor(Color.Grey35));
            return;
        }

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap().Width(14)); // label 1
        grid.AddColumn(new GridColumn().NoWrap().Width(22)); // value 1
        grid.AddColumn(new GridColumn().NoWrap().Width(12)); // label 2
        grid.AddColumn(new GridColumn().NoWrap().Width(22)); // value 2
        grid.AddColumn(new GridColumn().NoWrap().Width(10)); // label 3
        grid.AddColumn(new GridColumn());                    // value 3

        var status  = dev.IsActive ? "[bold green]● ONLINE[/]" : "[grey50]○ OFFLINE[/]";
        var ping    = dev.PingMs >= 0 ? $"[white]{dev.PingMs}ms[/]" : dev.IsActive ? "[yellow]FW BLOCK[/]" : "[grey50]--[/]";
        var mac     = string.IsNullOrEmpty(dev.MacAddress) ? "[grey50]--[/]" : $"[white]{dev.MacAddress}[/]";
        var vendor  = string.IsNullOrEmpty(dev.Vendor) || dev.Vendor == "Unknown"
            ? "[grey50]--[/]" : $"[cyan]{dev.Vendor}[/]";
        var typeStr = !string.IsNullOrEmpty(dev.DeviceType)
            ? dev.DeviceType
            : _typeDetector.Classify(dev);
        var type    = $"[yellow]{typeStr}[/]";
        var ttl     = dev.Ttl > 0 ? $"[grey84]{dev.Ttl}[/]" : "[grey50]--[/]";
        var host    = dev.Hostname == "Unknown" ? "[grey50]Unknown[/]" : $"[white]{dev.Hostname}[/]";

        // Row 1: IP | Hostname | Status | Ping | TTL
        grid.AddRow(
            "[bold cyan]IP Address[/]",   $"[bold white]{dev.IpAddress}[/]",
            "[bold cyan]Hostname[/]",      host,
            "[bold cyan]Status[/]",        status
        );
        // Row 2: MAC | Vendor | Type
        grid.AddRow(
            "[bold cyan]MAC Address[/]",  mac,
            "[bold cyan]Vendor[/]",        vendor,
            "[bold cyan]Type[/]",          type
        );
        // Row 3: Ping | TTL | Ports
        var portsStr = dev.OpenPorts.Count > 0
            ? string.Join("  ", dev.OpenPorts.Select(p => $"[green]{p}[/]"))
            : "[grey50]Press [bold]Enter[/] to scan ports[/]";
        grid.AddRow(
            "[bold cyan]Ping[/]",   ping,
            "[bold cyan]TTL[/]",    ttl,
            "[bold cyan]Ports[/]",  portsStr
        );

        var panel = new Panel(new Padder(grid, new Padding(1, 0)))
        {
            Border  = BoxBorder.Rounded,
            Padding = new Padding(0, 0),
            Header  = new PanelHeader($"[bold cyan] {dev.IpAddress} — {dev.Hostname} [/]")
        };
        panel.BorderColor(Color.Cyan1);
        AnsiConsole.Write(panel);
    }
}
