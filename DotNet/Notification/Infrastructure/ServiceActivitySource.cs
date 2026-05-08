using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Settings;
using System.Diagnostics;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Notification.Infrastructure
{
    public static class ServiceActivitySource
    {
        private static string _version = string.Empty;
        public static string ServiceName = NotificationConstants.ServiceName;
        public static ActivitySource Instance { get; private set; } = new ActivitySource(ServiceName, _version);

        public static void Initialize(ServiceInformation serviceInfo)
        {
            _version = serviceInfo.Version;
            Instance = new ActivitySource(ServiceName, _version);
        }
    }
}
