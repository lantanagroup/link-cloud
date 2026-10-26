using System.Diagnostics;

namespace LantanaGroup.Link.Shared.Application.Models;

public static class ServiceActivitySource
{
    public static string Version = string.Empty;
    public static string ProductVersion = string.Empty;
    public static string ServiceName = string.Empty;
    public static ActivitySource Instance { get; private set; } = new ActivitySource(ServiceName, Version);

    public static void Initialize(ServiceInformation serviceInfo)
    {
        ProductVersion = serviceInfo.ProductVersion ?? string.Empty;
        ServiceName = serviceInfo.ServiceConfigName ?? string.Empty;
        Version = serviceInfo.Version;
        Instance = new ActivitySource(ServiceName, Version);
    }

    public static void Initialize(string assemblyVersion, ServiceInformation? serviceInfo)
    {
        if (!string.IsNullOrEmpty(serviceInfo?.ServiceConfigName))
        {
            ServiceName = serviceInfo.ServiceConfigName;
        }

        ProductVersion = serviceInfo?.ProductVersion ?? string.Empty;

        // A ServiceInformation section without a Version used to leave this empty (LEGLINK-1420).
        Version = string.IsNullOrEmpty(serviceInfo?.Version) ? assemblyVersion : serviceInfo.Version;

        Instance = new ActivitySource(ServiceName, Version);
    }
}
