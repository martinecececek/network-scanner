# NetRadar — Network Intelligence Scanner

> A fast, keyboard-driven network scanner for your local network — built with C# .NET 8 and a rich console UI powered by Spectre.Console.

---

## Features

- **Auto-discover** every device on your local network (IP, hostname, MAC address)
- **Ping sweep** with live response times and TTL detection
- **Device classification** — Windows PC, Linux/Mac, Router, Printer, IoT, Mobile, and more
- **Vendor lookup** — identifies the manufacturer from the MAC address (OUI database)
- **Port scanner** — checks 14 common ports per device on demand
- **Security Scanner** — scans all active devices for dangerous open services and assigns a risk level (HIGH / MED / LOW / SAFE)
- **Inline detail pane** — see key device info without leaving the main table
- **Filter & sort** — show active-only, sort by IP / ping / hostname
- **Manual ping** — re-ping any device on the fly with `P`
- **Beautiful TUI** — color-coded table, animated progress bars, panel overlays

---

## Screenshots

```
╭─────────────────────────────────────────────────────────────────────────────╮
│  NetRadar — Network Scanner                                                 │
├────┬────────────────┬──────────────────────┬────────────┬──────┬────────────┤
│  # │ IP Address     │ Hostname             │ Status     │ Risk │ Ping       │
├────┼────────────────┼──────────────────────┼────────────┼──────┼────────────┤
│  1 │ 192.168.1.1    │ router.local         │ ● ONLINE   │ MED  │ 2ms        │
│► 2 │ 192.168.1.5    │ DESKTOP-ABC123       │ ● ONLINE   │ SAFE │ 1ms        │
│  3 │ 192.168.1.12   │ android-phone        │ ● ONLINE   │ --   │ 8ms        │
│  4 │ 192.168.1.20   │ Unknown              │ ○ OFFLINE  │ --   │ --         │
╰────┴────────────────┴──────────────────────┴────────────┴──────┴────────────╯
╭─ 192.168.1.5 — DESKTOP-ABC123 ────────────────────────────────────────────╮
│  IP Address    192.168.1.5      Hostname   DESKTOP-ABC123   Status  ● ONLINE│
│  MAC Address   A4:C3:F0:xx:xx  Vendor     Intel Corp        Type    Win PC  │
│  Ping          1ms             TTL        128               Ports   135 445 │
╰────────────────────────────────────────────────────────────────────────────╯
 ↑↓ Navigate  Enter Details  P Ping  F Filter  R Rescan  S Security  Tab Sort
```

---

## Requirements

| Requirement | Version |
|---|---|
| .NET SDK | 8.0 or later |
| OS | Windows 10/11 |
| Terminal width | 100+ columns recommended |

> **Note:** MAC address and ARP discovery requires the app to be run on Windows. The ARP table is read via `arp -a` and the `SendARP` Win32 API.

---

## Build & Run

```bash
# Clone or download the project
cd network-scanner

# Run directly
dotnet run --project NetRadar.csproj

# Or build a release binary
dotnet build NetRadar.csproj -c Release
.\bin\Release\net8.0\NetRadar.exe
```

---

## Keyboard Shortcuts

### Main Table

| Key | Action |
|---|---|
| `↑` / `↓` | Navigate devices |
| `Enter` | Open full Device Detail (triggers port scan) |
| `P` | Ping the selected device |
| `F` | Toggle active-only filter |
| `Tab` | Cycle sort: IP → Ping → Hostname |
| `R` / `F5` | Rescan the whole network |
| `S` | Run Security Scanner on all active devices |
| `Q` / `Esc` | Quit |

### Device Detail Screen

| Key | Action |
|---|---|
| `R` | Re-scan ports |
| `B` / `Esc` | Back to main table |
| `Q` | Quit |

### Security Scan Results

| Key | Action |
|---|---|
| `↑` / `↓` | Navigate results |
| `Enter` | Open Device Detail for selected |
| `B` / `Esc` | Back to main table |
| `Q` | Quit |

---

## Security Scanner

Press `S` from the main table to launch the security scanner. It will:

1. Run a full port scan on every active device in parallel
2. Classify each device's open ports against a risk ruleset
3. Show a color-coded report sorted by risk level

### Risk Levels

| Level | Color | Triggered by |
|---|---|---|
| **HIGH** | Red | Telnet (23), FTP (21), VNC (5900) |
| **MED** | Orange | RDP (3389), SMB (445), NetBIOS (139) |
| **LOW** | Yellow | HTTP without HTTPS (80), RPC (135), SMTP (25) |
| **SAFE** | Green | No risky ports detected |

After scanning, the `Risk` column in the main table updates permanently for that session.

---

## Device Detection

NetRadar uses a three-layer approach to find devices:

1. **ICMP ping sweep** — fast parallel sweep with 300ms timeout
2. **ARP cache read** — catches devices that block ICMP but are visible at Layer 2
3. **TCP fallback** — connects to ports 135/445/80 to confirm devices behind firewalls

Device types are classified using a priority ruleset based on open ports, TTL, and hostname patterns:

| Type | Detection logic |
|---|---|
| Printer | Port 9100 (JetDirect) open |
| Windows PC | Port 3389 open, or TTL = 128 |
| Linux / Mac | Port 22 open + TTL = 64, or TTL = 64 |
| Network Device | TTL = 255 |
| Router / Switch | Port 23 open, or hostname contains `router`, `fritz`, etc. |
| IoT / Server | Port 80 or 443 open |
| Mobile | Hostname contains `iphone`, `android`, `galaxy`, etc. |

---

## Configuration

Edit `appsettings.json` to tune scanner behavior:

```json
{
  "Scanner": {
    "PingTimeoutMs": 300,
    "PingConcurrency": 80,
    "DnsTimeoutMs": 600,
    "PortScanTimeoutMs": 300,
    "PortScanConcurrency": 20,
    "Ports": [21, 22, 23, 25, 53, 80, 135, 139, 443, 445, 3389, 5900, 8080, 9100]
  }
}
```

| Setting | Description |
|---|---|
| `PingTimeoutMs` | ICMP timeout per host (ms) |
| `PingConcurrency` | Max parallel pings |
| `DnsTimeoutMs` | Hostname resolution timeout (ms) |
| `PortScanTimeoutMs` | TCP connect timeout per port (ms) |
| `PortScanConcurrency` | Max parallel port checks per device |
| `Ports` | List of ports to scan |

---

## Project Structure

```
NetRadar/
├── Core/
│   ├── Models/
│   │   ├── NetworkDevice.cs       # Device data model + RiskLevel enum
│   │   ├── ScannerOptions.cs      # Config binding
│   │   └── ScanResult.cs
│   └── Services/
│       ├── NetworkDiscovery.cs    # Ping sweep, ARP, TCP fallback, DNS
│       ├── PortScanner.cs         # TCP port scanner
│       ├── DeviceTypeDetector.cs  # Classifies device type
│       ├── VendorLookup.cs        # MAC OUI → vendor name
│       └── SecurityAnalyzer.cs   # Risk scoring from open ports
├── UI/
│   ├── AppShell.cs                # App state machine
│   ├── Components/
│   │   ├── ScanProgressDisplay.cs # Animated scan progress bars
│   │   ├── StatusBar.cs           # Bottom key-hint bar
│   │   └── ThemeColors.cs
│   └── Screens/
│       ├── SplashScreen.cs        # Startup interface info screen
│       ├── MainTableScreen.cs     # Main device list + inline detail pane
│       ├── DeviceDetailScreen.cs  # Full device detail + port scan
│       └── SecurityScanScreen.cs  # Security sweep + results
├── Resources/
│   └── oui.txt                    # MAC OUI vendor database (embedded)
├── appsettings.json
└── Program.cs
```

---

## Tech Stack

- **[.NET 8](https://dotnet.microsoft.com/)** — C# console application
- **[Spectre.Console](https://spectreconsole.net/)** — rich terminal UI (tables, panels, progress bars)
- **[Microsoft.Extensions.Hosting](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host)** — dependency injection & configuration
- **System.Net.NetworkInformation** — ICMP ping
- **System.Net.Sockets** — TCP port scanning
- **iphlpapi.dll / SendARP** — direct MAC address resolution (P/Invoke)

---

## License

MIT — do whatever you want with it.
