using QueryDispatch.Application.Models;
using QueryDispatch.Application.Settings;
using System.Diagnostics;

namespace QueryDispatch.Application.Services
{
    public class ServiceActivitySource
    {
        private static string _version = string.Empty;
        public static string ServiceName = QueryDispatchConstants.ServiceName;
        public static ActivitySource Instance { get; private set; } = new ActivitySource(ServiceName, _version);

        public static void Initialize(ServiceInformation serviceInfo)
        {
            _version = serviceInfo.Version;
            Instance = new ActivitySource(ServiceName, _version);
        }
    }
}
