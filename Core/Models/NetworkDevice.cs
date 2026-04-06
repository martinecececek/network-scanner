namespace NetRadar.Core.Models;

public enum RiskLevel { Unknown, Safe, Low, Medium, High }

public class NetworkDevice
{
    public string IpAddress  { get; set; } = "";
    public string Hostname   { get; set; } = "Unknown";
    public bool   IsActive   { get; set; }
    public long   PingMs     { get; set; } = -1;   // -1 = unreachable
    public string MacAddress { get; set; } = "";
    public string Vendor     { get; set; } = "";
    public string DeviceType { get; set; } = "";
    public int    Ttl        { get; set; }
    public List<int> OpenPorts { get; set; } = new();
    public DateTime LastSeen { get; set; }
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Unknown;
    public List<string> SecurityFindings { get; set; } = new();
}
