namespace NetRadar.Core.Models;

public class ScannerOptions
{
    public int PingTimeoutMs       { get; set; } = 800;
    public int PingConcurrency     { get; set; } = 50;
    public int DnsTimeoutMs        { get; set; } = 1000;
    public int PortScanTimeoutMs   { get; set; } = 300;
    public int PortScanConcurrency { get; set; } = 20;
    public int[] Ports { get; set; } = { 22, 23, 25, 53, 80, 135, 139, 443, 445, 3389, 8080, 9100 };
}
