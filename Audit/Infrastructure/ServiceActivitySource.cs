using LantanaGroup.Link.Audit.Application.Models;
using System.Diagnostics;

namespace LantanaGroup.Link.Audit.Infrastructure
{
    public static class ServiceActivitySource
    {
        private static string _version = string.Empty;
        public static string ServiceName = "Link Audit Service";
        public static ActivitySource Instance { get; private set; } = new ActivitySource(ServiceName, _version);

        public static void Initialize(ServiceInformation serviceInfo)
        {
            ServiceName = serviceInfo.Name;
            _version = serviceInfo.Version;
            Instance = new ActivitySource(ServiceName, _version);
        }
    }
}
