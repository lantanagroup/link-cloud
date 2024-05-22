using System.Diagnostics;
using LantanaGroup.Link.DemoApiGateway.Application.models;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.DemoApiGateway.Infrastructure
{
    public static class ServiceActivitySource
    {      
        private static string _version = string.Empty;
        public static string ServiceName = "Demo Api Gateway";
        public static ActivitySource Instance { get; private set; } = new ActivitySource(ServiceName, _version);

        public static void Initialize(ServiceInformation serviceInfo)
        {
            ServiceName = serviceInfo.Name;
            _version = serviceInfo.Version;
            Instance = new ActivitySource(ServiceName, _version);
        }
    }
}
