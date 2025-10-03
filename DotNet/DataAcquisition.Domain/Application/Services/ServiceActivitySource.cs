using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using System.Diagnostics;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services
{
    public class ServiceActivitySource
    {
        private static string _version = string.Empty;
        public static string ServiceName = string.Empty;
        public static ActivitySource Instance { get; private set; } = new ActivitySource(ServiceName, _version);

        public static void Initialize(ServiceInformation serviceInfo)
        {
            _version = serviceInfo.Version;
            ServiceName = serviceInfo.ServiceName ?? DataAcquisitionConstants.ServiceName;
            Instance = new ActivitySource(ServiceName, _version);
        }
    }
}
