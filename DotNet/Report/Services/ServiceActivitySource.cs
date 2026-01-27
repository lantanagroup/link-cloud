using LantanaGroup.Link.Shared.Application.Models;
using System.Diagnostics;

namespace LantanaGroup.Link.Report.Services
{
    public static class ServiceActivitySource
    {
        private static string _version = string.Empty;
        public static string ProductVersion = string.Empty;
        public static string ServiceName = string.Empty;

        public static ActivitySource Instance { get; private set; } = new ActivitySource(ServiceName, _version);

        public static void Initialize(ServiceInformation serviceInfo)
        {
            ServiceName = serviceInfo.ServiceConfigName;
            _version = serviceInfo.Version;
            ProductVersion = serviceInfo.ProductVersion ?? string.Empty;
            Instance = new ActivitySource(ServiceName, _version);
        }
    }
}
