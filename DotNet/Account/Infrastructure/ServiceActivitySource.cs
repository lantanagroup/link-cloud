using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Settings;
using System.Diagnostics;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Account.Infrastructure
{
    public static class ServiceActivitySource
    {
        public static string Version = string.Empty;
        public static string ServiceName = AccountConstants.ServiceName;
        public static ActivitySource Instance { get; private set; } = new ActivitySource(ServiceName, Version);

        public static void Initialize(string assemblyVersion, ServiceInformation? serviceInfo)
        {
            if (serviceInfo != null)
            {
                if (!string.IsNullOrEmpty(serviceInfo.ServiceName))
                    ServiceName = serviceInfo.ServiceName;
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
}
