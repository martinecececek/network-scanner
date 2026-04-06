namespace NetRadar.Core.Models;

public class ScanResult
{
    public List<NetworkDevice> Devices    { get; set; } = new();
    public DateTime ScanStarted           { get; set; }
    public DateTime ScanCompleted         { get; set; }
    public string SubnetScanned           { get; set; } = "";
    public int TotalHostsProbed           { get; set; }
}
