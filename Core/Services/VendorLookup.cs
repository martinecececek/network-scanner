using System.Reflection;

namespace NetRadar.Core.Services;

public class VendorLookup
{
    private readonly Dictionary<string, string> _oui;

    public VendorLookup()
    {
        _oui = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        LoadOuiDatabase();
    }

    private void LoadOuiDatabase()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "NetRadar.Resources.oui.txt";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return;

        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;

            var idx = line.IndexOf(',');
            if (idx < 0) continue;

            var prefix = line[..idx].Trim().ToUpperInvariant();
            var vendor = line[(idx + 1)..].Trim();

            _oui.TryAdd(prefix, vendor);
        }
    }

    public string Lookup(string mac)
    {
        if (string.IsNullOrEmpty(mac) || mac.Length < 8)
            return "Unknown";

        // Normalize separators: accept both ':' and '-'
        var normalized = mac.Replace('-', ':').ToUpperInvariant();
        var prefix = normalized[..8]; // "XX:XX:XX"

        return _oui.TryGetValue(prefix, out var vendor) ? vendor : "Unknown";
    }
}
