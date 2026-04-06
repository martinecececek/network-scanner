using NetRadar.Core.Models;
using NetRadar.Core.Services;
using NetRadar.UI.Components;
using NetRadar.UI.Screens;
using Spectre.Console;

namespace NetRadar.UI;

public class AppShell
{
    private readonly NetworkDiscovery _discovery;
    private readonly PortScanner _portScanner;
    private readonly DeviceTypeDetector _typeDetector;
    private readonly SecurityAnalyzer _securityAnalyzer;

    private List<NetworkDevice> _devices = new();
    private readonly MainTableScreen _mainTable;

    public AppShell(
        NetworkDiscovery discovery,
        PortScanner portScanner,
        DeviceTypeDetector typeDetector,
        SecurityAnalyzer securityAnalyzer)
    {
        _discovery       = discovery;
        _portScanner     = portScanner;
        _typeDetector    = typeDetector;
        _securityAnalyzer = securityAnalyzer;
        _mainTable       = new MainTableScreen(typeDetector);
    }

    public async Task RunAsync()
    {
        // Ensure console is set up nicely
        Console.Title = "NetRadar — Network Scanner";
        Console.CursorVisible = false;
        try
        {
            // Check minimum window width
            if (Console.WindowWidth < 100)
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] NetRadar works best at 100+ columns wide. Current: [bold]{0}[/]. Please widen your terminal.", Console.WindowWidth);
                Thread.Sleep(2000);
            }

            // Show splash (also displays all interfaces for debugging)
            SplashScreen.Show(_discovery);

            // Scan + main loop
            await ScanAndLoop();
        }
        finally
        {
            Console.CursorVisible = true;
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[cyan]NetRadar[/] [grey50]exited. Goodbye![/]");
        }
    }

    private async Task ScanAndLoop()
    {
        var result = await ScanProgressDisplay.RunWithProgressAsync(_discovery);
        _devices = result.Devices;
        _mainTable.ResetSelection();

        await MainLoop();
    }

    private async Task MainLoop()
    {
        while (true)
        {
            var (action, selected) = await _mainTable.RunAsync(_devices, _discovery);

            switch (action)
            {
                case NavAction.OpenDetail when selected != null:
                    var detailAction = await DeviceDetailScreen.ShowAsync(selected, _portScanner, _typeDetector);
                    if (detailAction == DetailAction.Quit)
                        return;
                    break;

                case NavAction.SecurityScan:
                    await SecurityScanScreen.ScanAllAsync(_devices, _portScanner, _securityAnalyzer, _typeDetector);
                    while (true)
                    {
                        var (secAction, secDevice) = SecurityScanScreen.ShowResults(_devices);
                        if (secAction == SecurityScanAction.OpenDetail && secDevice != null)
                        {
                            var secDetail = await DeviceDetailScreen.ShowAsync(secDevice, _portScanner, _typeDetector);
                            if (secDetail == DetailAction.Quit) return;
                        }
                        else if (secAction == SecurityScanAction.Quit)
                            return;
                        else
                            break;
                    }
                    break;

                case NavAction.Rescan:
                    await ScanAndLoop();
                    return;

                case NavAction.Quit:
                    return;
            }
        }
    }
}
