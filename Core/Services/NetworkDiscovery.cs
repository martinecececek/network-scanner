using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using NetRadar.Core.Models;

namespace NetRadar.Core.Services;

public class NetworkDiscovery
{
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int SendARP(uint destIp, uint srcIp, byte[] macAddr, ref uint phyAddrLen);

    private readonly ScannerOptions _opts;
    private readonly VendorLookup _vendorLookup;

    private static readonly int[] FallbackPorts = { 135, 445, 80 };

    public NetworkDiscovery(IOptions<ScannerOptions> opts, VendorLookup vendorLookup)
    {
        _opts = opts.Value;
        _vendorLookup = vendorLookup;
    }

    public List<(string name, string ip, string subnet, bool hasGateway, bool isVirtual)> GetAllInterfaces()
    {
        var result = new List<(string, string, string, bool, bool)>();
        foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (iface.OperationalStatus != OperationalStatus.Up) continue;
            if (iface.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

            var desc = (iface.Name + " " + iface.Description).ToLowerInvariant();
            var isVirtual = desc.Contains("hyper-v") || desc.Contains("vmware") ||
                            desc.Contains("virtualbox") || desc.Contains("vethernet") ||
                            desc.Contains("pseudo") || desc.Contains("teredo") || desc.Contains("isatap");

            var props = iface.GetIPProperties();
            var hasGateway = props.GatewayAddresses.Any(g =>
                g.Address.AddressFamily == AddressFamily.InterNetwork &&
                g.Address.ToString() != "0.0.0.0");

            foreach (var addr in props.UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var ip = addr.Address.ToString();
                if (ip.StartsWith("127.")) continue;
                var parts = ip.Split('.');
                result.Add((iface.Description, ip, $"{parts[0]}.{parts[1]}.{parts[2]}", hasGateway, isVirtual));
            }
        }
        return result;
    }

    public (string localIp, string subnet) DetectLocalNetwork()
    {
        var candidates = new List<(string ip, string subnet, int priority)>();

        foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (iface.OperationalStatus != OperationalStatus.Up) continue;
            if (iface.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

            var desc = (iface.Name + " " + iface.Description).ToLowerInvariant();
            if (desc.Contains("hyper-v") || desc.Contains("vmware") || desc.Contains("virtualbox") ||
                desc.Contains("vethernet") || desc.Contains("pseudo") || desc.Contains("teredo") ||
                desc.Contains("isatap") || desc.Contains("loopback adapter")) continue;

            var props = iface.GetIPProperties();
            var hasGateway = props.GatewayAddresses.Any(g =>
                g.Address.AddressFamily == AddressFamily.InterNetwork &&
                g.Address.ToString() != "0.0.0.0");

            foreach (var addr in props.UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var ip = addr.Address.ToString();
                if (!IsPrivateIp(ip)) continue;

                var parts = ip.Split('.');
                var subnet = $"{parts[0]}.{parts[1]}.{parts[2]}";

                var priority = (hasGateway ? 0 : 100) + iface.NetworkInterfaceType switch
                {
                    NetworkInterfaceType.Ethernet or
                    NetworkInterfaceType.GigabitEthernet or
                    NetworkInterfaceType.FastEthernetFx or
                    NetworkInterfaceType.FastEthernetT => 0,
                    NetworkInterfaceType.Wireless80211  => 10,
                    _                                   => 50
                };
                candidates.Add((ip, subnet, priority));
            }
        }

        if (candidates.Count > 0)
        {
            var best = candidates.OrderBy(c => c.priority).First();
            return (best.ip, best.subnet);
        }
        return ("127.0.0.1", "127.0.0");
    }

    private static bool IsPrivateIp(string ip)
    {
        if (ip.StartsWith("10.")) return true;
        if (ip.StartsWith("192.168.")) return true;
        if (ip.StartsWith("172."))
        {
            var second = int.Parse(ip.Split('.')[1]);
            return second >= 16 && second <= 31;
        }
        return false;
    }

    public async Task<ScanResult> ScanAsync(
        IProgress<(int completed, int total)> pingProgress,
        IProgress<(int completed, int total)> dnsProgress)
    {
        var (_, subnet) = DetectLocalNetwork();
        var hosts = Enumerable.Range(1, 254).Select(i => $"{subnet}.{i}").ToList();

        var result = new ScanResult
        {
            ScanStarted      = DateTime.Now,
            SubnetScanned    = $"{subnet}.0/24",
            TotalHostsProbed = hosts.Count
        };

        var devices    = hosts.Select(ip => new NetworkDevice { IpAddress = ip, LastSeen = DateTime.Now }).ToList();
        var deviceByIp = devices.ToDictionary(d => d.IpAddress);

        // ── Phase 1: ICMP ping sweep ──────────────────────────────────────────
        // Pinging every host also causes the OS to run ARP at Layer 2, so by
        // the time each ping times out the ARP cache already has the MAC for
        // any host that is alive (even if its firewall drops the ICMP reply).
        var pingSem = new SemaphoreSlim(_opts.PingConcurrency);
        int pingDone = 0;

        await Task.WhenAll(hosts.Select(async ip =>
        {
            await pingSem.WaitAsync();
            try
            {
                var (active, ms, ttl) = await PingHostAsync(ip, _opts.PingTimeoutMs);
                var dev = deviceByIp[ip];
                dev.IsActive = active;
                dev.PingMs   = ms;
                dev.Ttl      = ttl;
            }
            finally
            {
                pingProgress.Report((Interlocked.Increment(ref pingDone), hosts.Count));
                pingSem.Release();
            }
        }));

        // ── Phase 2: ARP table — instant read of OS cache ────────────────────
        // After the ping sweep the cache is populated for all live hosts.
        // Any host that responded to ARP (even if ICMP was blocked) appears here.
        var arpTable = await GetArpTableAsync();
        foreach (var dev in devices)
        {
            if (!arpTable.TryGetValue(dev.IpAddress, out var mac)) continue;
            dev.MacAddress = mac;
            dev.Vendor     = _vendorLookup.Lookup(mac);
            dev.IsActive   = true;   // ARP entry = host is alive
        }

        // ── Phase 2b: SendARP for active hosts that ARP table missed ─────────
        // Only runs for hosts already confirmed active (returns from cache = fast).
        var arpSem = new SemaphoreSlim(30);
        await Task.WhenAll(devices.Where(d => d.IsActive && string.IsNullOrEmpty(d.MacAddress)).Select(async dev =>
        {
            await arpSem.WaitAsync();
            try
            {
                var mac = await Task.Run(() => GetMacViaSendArp(dev.IpAddress));
                if (mac == null) return;
                dev.MacAddress = mac;
                dev.Vendor     = _vendorLookup.Lookup(mac);
            }
            finally { arpSem.Release(); }
        }));

        // ── Phase 3: TCP fallback for hosts with no ICMP and no ARP ──────────
        // Limited to 3 ports with a short timeout — only catches edge cases.
        var tcpSem = new SemaphoreSlim(30);
        await Task.WhenAll(devices.Where(d => !d.IsActive).Select(async dev =>
        {
            await tcpSem.WaitAsync();
            try
            {
                if (await TcpProbeAsync(dev.IpAddress, FallbackPorts, 150))
                    dev.IsActive = true;
            }
            finally { tcpSem.Release(); }
        }));

        // ── Phase 4: DNS for active devices ──────────────────────────────────
        var activeDevices = devices.Where(d => d.IsActive).ToList();
        var dnsSem  = new SemaphoreSlim(20);
        int dnsDone = 0;

        await Task.WhenAll(activeDevices.Select(async dev =>
        {
            await dnsSem.WaitAsync();
            try
            {
                using var cts = new CancellationTokenSource(_opts.DnsTimeoutMs);
                try
                {
                    var entry = await Dns.GetHostEntryAsync(dev.IpAddress, cts.Token);
                    if (!string.IsNullOrEmpty(entry.HostName) && entry.HostName != dev.IpAddress)
                        dev.Hostname = entry.HostName.Split('.')[0];
                }
                catch { }
            }
            finally
            {
                dnsProgress.Report((Interlocked.Increment(ref dnsDone), activeDevices.Count));
                dnsSem.Release();
            }
        }));

        result.Devices       = devices.OrderBy(d => IpToLong(d.IpAddress)).ToList();
        result.ScanCompleted = DateTime.Now;
        return result;
    }

    public async Task<(bool isActive, long pingMs, int ttl)> PingOneAsync(string ip)
    {
        var (active, ms, ttl) = await PingHostAsync(ip, _opts.PingTimeoutMs);
        if (active) return (true, ms, ttl);

        var arpTable = await GetArpTableAsync();
        if (arpTable.ContainsKey(ip)) return (true, -1, 0);

        var mac = await Task.Run(() => GetMacViaSendArp(ip));
        if (mac != null) return (true, -1, 0);

        if (await TcpProbeAsync(ip, FallbackPorts, 300)) return (true, -1, 0);
        return (false, -1, 0);
    }

    private static async Task<(bool isActive, long pingMs, int ttl)> PingHostAsync(string ip, int timeoutMs)
    {
        using var ping = new Ping();
        try
        {
            var reply = await ping.SendPingAsync(ip, timeoutMs);
            if (reply.Status == IPStatus.Success)
                return (true, reply.RoundtripTime, reply.Options?.Ttl ?? 0);
        }
        catch { }
        return (false, -1, 0);
    }

    private static string? GetMacViaSendArp(string ip)
    {
        try
        {
            var ipBytes = IPAddress.Parse(ip).GetAddressBytes();
            var destIp  = BitConverter.ToUInt32(ipBytes, 0);
            var mac     = new byte[6];
            var macLen  = (uint)6;
            var ret     = SendARP(destIp, 0, mac, ref macLen);
            if (ret == 0 && macLen == 6 && mac.Any(b => b != 0))
                return string.Join(":", mac.Select(b => b.ToString("X2")));
        }
        catch { }
        return null;
    }

    private static async Task<Dictionary<string, string>> GetArpTableAsync()
    {
        var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var psi = new ProcessStartInfo("arp", "-a")
            {
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                StandardOutputEncoding = Encoding.Default  // handles OEM/locale output
            };
            using var proc = Process.Start(psi)!;
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            var macRegex = new Regex(@"^[\da-fA-F]{2}[-:][\da-fA-F]{2}[-:][\da-fA-F]{2}[-:][\da-fA-F]{2}[-:][\da-fA-F]{2}[-:][\da-fA-F]{2}$");

            foreach (var line in output.Split('\n'))
            {
                // Split on any whitespace, take first two non-empty tokens
                var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                var ip  = parts[0];
                var mac = parts[1];

                if (!IPAddress.TryParse(ip, out _)) continue;
                if (!macRegex.IsMatch(mac)) continue;

                mac = mac.Replace('-', ':').ToUpperInvariant();
                if (mac.StartsWith("FF:FF") || mac == "00:00:00:00:00:00") continue;

                table.TryAdd(ip, mac);
            }
        }
        catch { }
        return table;
    }

    private static async Task<bool> TcpProbeAsync(string ip, int[] ports, int timeoutMs)
    {
        var tasks = ports.Select(async port =>
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            try { using var tcp = new TcpClient(); await tcp.ConnectAsync(ip, port, cts.Token); return true; }
            catch { return false; }
        });
        return (await Task.WhenAll(tasks)).Any(r => r);
    }

    private static long IpToLong(string ip)
    {
        try { return ip.Split('.').Select(long.Parse).Aggregate((a, b) => a * 256 + b); }
        catch { return 0; }
    }
}
