using System.Net.Sockets;
using Microsoft.Extensions.Options;
using NetRadar.Core.Models;

namespace NetRadar.Core.Services;

public class PortScanner
{
    private static readonly Dictionary<int, string> PortNames = new()
    {
        [21]   = "FTP",
        [22]   = "SSH",
        [23]   = "Telnet",
        [25]   = "SMTP",
        [53]   = "DNS",
        [80]   = "HTTP",
        [110]  = "POP3",
        [135]  = "RPC",
        [139]  = "NetBIOS",
        [443]  = "HTTPS",
        [445]  = "SMB",
        [3389] = "RDP",
        [8080] = "HTTP-Alt",
        [9100] = "JetDirect"
    };

    private readonly ScannerOptions _opts;

    public PortScanner(IOptions<ScannerOptions> opts) => _opts = opts.Value;

    public static string GetPortName(int port) =>
        PortNames.TryGetValue(port, out var name) ? name : "Unknown";

    public async Task<List<int>> ScanAsync(string ip)
    {
        var semaphore = new SemaphoreSlim(_opts.PortScanConcurrency);
        var tasks = _opts.Ports.Select(async port =>
        {
            await semaphore.WaitAsync();
            try
            {
                return await IsPortOpenAsync(ip, port) ? port : -1;
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.Where(p => p > 0).OrderBy(p => p).ToList();
    }

    private async Task<bool> IsPortOpenAsync(string ip, int port)
    {
        using var cts = new CancellationTokenSource(_opts.PortScanTimeoutMs);
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(ip, port, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
