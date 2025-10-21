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
        if (serviceInfo != null)
        {
            if (!string.IsNullOrEmpty(serviceInfo.ServiceConfigName))
                ServiceName = serviceInfo.ServiceConfigName;
            if (!string.IsNullOrEmpty(serviceInfo.Version))
                Version = serviceInfo.Version;
        }
        else
        {
            Version = assemblyVersion;
        }

        Instance = new ActivitySource(ServiceName, Version);
    }
}
