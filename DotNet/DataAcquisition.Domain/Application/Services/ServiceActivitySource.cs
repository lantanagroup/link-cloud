using LantanaGroup.Link.Shared.Application.Models;
using System.Diagnostics;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services
{
    public class ServiceActivitySource
    {
        private static string _version = string.Empty;
        public static string ServiceName { get; private set; } = string.Empty;
        public static ActivitySource Instance { get; private set; } = null!;

        public static void Initialize(string serviceName, ServiceInformation serviceInfo)
        {
            ServiceName = serviceName;
            _version = serviceInfo.Version;
            Instance = new ActivitySource(ServiceName, _version);
        }
    }
}
