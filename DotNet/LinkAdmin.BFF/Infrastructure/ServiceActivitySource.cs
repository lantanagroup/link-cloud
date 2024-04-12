using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using System.Diagnostics;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure
{
    public static class ServiceActivitySource
    {
        private static string _version = string.Empty;
        public static string ServiceName = "Link Admin BFF";
        public static ActivitySource Instance { get; private set; } = new ActivitySource(ServiceName, _version);

        public static void Initialize(ServiceInformation serviceInfo)
        {
            ServiceName = serviceInfo.Name;
            _version = serviceInfo.Version;
            Instance = new ActivitySource(ServiceName, _version);
        }
    }
}
