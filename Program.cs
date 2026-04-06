using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetRadar.Core.Models;
using NetRadar.Core.Services;
using NetRadar.UI;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        services.Configure<ScannerOptions>(ctx.Configuration.GetSection("Scanner"));
        services.AddSingleton<VendorLookup>();
        services.AddSingleton<NetworkDiscovery>();
        services.AddSingleton<PortScanner>();
        services.AddSingleton<DeviceTypeDetector>();
        services.AddSingleton<SecurityAnalyzer>();
        services.AddSingleton<AppShell>();
    })
    .Build();

var shell = host.Services.GetRequiredService<AppShell>();
await shell.RunAsync();
