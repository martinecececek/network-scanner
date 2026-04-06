using Spectre.Console;
using NetRadar.Core.Models;
using NetRadar.Core.Services;

namespace NetRadar.UI.Components;

public static class ScanProgressDisplay
{
    public static async Task<ScanResult> RunWithProgressAsync(NetworkDiscovery discovery)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("NetRadar").Color(Color.Cyan1).Centered());
        AnsiConsole.WriteLine();

        ScanResult? result = null;

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
                var pingTask = ctx.AddTask("[cyan]Pinging hosts[/]",        maxValue: 254);
                var dnsTask  = ctx.AddTask("[cyan]Resolving hostnames[/]",  maxValue: 254);

                // DNS task waits until ping phase finishes
                dnsTask.IsIndeterminate = true;
                dnsTask.StopTask();

                var pingProgress = new Progress<(int completed, int total)>(p =>
                {
                    pingTask.MaxValue = p.total;
                    pingTask.Value    = p.completed;
                });

                var dnsProgress = new Progress<(int completed, int total)>(p =>
                {
                    if (!dnsTask.IsStarted)
                    {
                        dnsTask.MaxValue        = p.total;
                        dnsTask.IsIndeterminate = false;
                        dnsTask.StartTask();
                    }
                    dnsTask.MaxValue = p.total;
                    dnsTask.Value    = p.completed;
                });

                result = await discovery.ScanAsync(pingProgress, dnsProgress);

                // Ensure both bars reach 100 %
                pingTask.Value = pingTask.MaxValue;
                if (!dnsTask.IsStarted) { dnsTask.IsIndeterminate = false; dnsTask.StartTask(); }
                dnsTask.Value = dnsTask.MaxValue;
            });

        var active = result!.Devices.Count(d => d.IsActive);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            $"[bold green] Scan complete![/] [white]Found [bold]{active}[/] active device(s) " +
            $"out of [bold]{result.TotalHostsProbed}[/] hosts.[/]");
        AnsiConsole.MarkupLine(
            $"[grey50] Subnet: {result.SubnetScanned}  |  " +
            $"Duration: {(result.ScanCompleted - result.ScanStarted).TotalSeconds:F1}s[/]");
        Thread.Sleep(1200);

        return result;
    }
}
