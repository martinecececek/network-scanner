using NetRadar.Core.Models;

namespace NetRadar.Core.Services;

public class DeviceTypeDetector
{
    public string Classify(NetworkDevice device)
    {
        var ports = device.OpenPorts;
        var hostname = device.Hostname.ToLowerInvariant();
        var ttl = device.Ttl;

        // Priority-ordered rules — first match wins
        if (ports.Contains(9100))
            return "Printer";

        if (ports.Contains(3389))
            return "Windows PC";

        if (ports.Contains(22) && ttl == 64)
            return "Linux / Mac";

        if (ttl == 255)
            return "Network Device";

        if (ports.Contains(23))
            return "Router / Switch";

        if (ContainsAny(hostname, "router", "gateway", "gw", "fritz", "draytek"))
            return "Router";

        if (ContainsAny(hostname, "printer", "print", "hp", "brother", "epson", "canon", "lexmark"))
            return "Printer";

        if (ContainsAny(hostname, "iphone", "ipad", "android", "pixel", "galaxy", "phone"))
            return "Mobile";

        if (ports.Contains(80) || ports.Contains(443))
            return "IoT / Server";

        if (ttl == 128)
            return "Windows PC";

        if (ttl == 64)
            return "Linux / Mac";

        return "Device";
    }

    private static bool ContainsAny(string source, params string[] terms) =>
        terms.Any(t => source.Contains(t, StringComparison.OrdinalIgnoreCase));
}
