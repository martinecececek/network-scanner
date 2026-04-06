using NetRadar.Core.Models;

namespace NetRadar.Core.Services;

public class SecurityAnalyzer
{
    public void Analyze(NetworkDevice device)
    {
        var findings = new List<string>();
        var level    = RiskLevel.Safe;

        void Flag(RiskLevel risk, string msg)
        {
            findings.Add(msg);
            if (risk > level) level = risk;
        }

        var ports = device.OpenPorts;

        // HIGH — unencrypted or highly exposed services
        if (ports.Contains(23))   Flag(RiskLevel.High,   "Telnet (23) — unencrypted remote access");
        if (ports.Contains(21))   Flag(RiskLevel.High,   "FTP (21) — unencrypted file transfer");
        if (ports.Contains(5900)) Flag(RiskLevel.High,   "VNC (5900) — remote desktop exposed");

        // MEDIUM — remote access / file sharing
        if (ports.Contains(3389)) Flag(RiskLevel.Medium, "RDP (3389) — remote desktop exposed");
        if (ports.Contains(445))  Flag(RiskLevel.Medium, "SMB (445) — file sharing exposed");
        if (ports.Contains(139))  Flag(RiskLevel.Medium, "NetBIOS (139) — legacy file sharing");

        // LOW — unencrypted web or minor exposure
        if (ports.Contains(80) && !ports.Contains(443))
            Flag(RiskLevel.Low, "HTTP (80) without HTTPS — no TLS");
        if (ports.Contains(135))  Flag(RiskLevel.Low,    "RPC (135) — Windows endpoint mapper");
        if (ports.Contains(25))   Flag(RiskLevel.Low,    "SMTP (25) — potential mail relay");

        device.RiskLevel        = level;
        device.SecurityFindings = findings;
    }
}
