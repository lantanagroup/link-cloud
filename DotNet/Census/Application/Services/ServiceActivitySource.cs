using LantanaGroup.Link.Census.Application.Settings;
using System.Diagnostics;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Census.Application.Services
{
    public static class ServiceActivitySource
    {
        private static string _version = string.Empty;
        public static string ServiceName = CensusConstants.ServiceName;
        public static ActivitySource Instance { get; private set; } = new ActivitySource(ServiceName, _version);

        public static void Initialize(ServiceInformation serviceInfo)
        {
            _version = serviceInfo.Version;
            Instance = new ActivitySource(ServiceName, _version);
        }
    }
}
