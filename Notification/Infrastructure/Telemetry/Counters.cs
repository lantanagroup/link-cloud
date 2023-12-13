using LantanaGroup.Link.Notification.Application.Models;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Notification.Infrastructure.Telemetry
{
    public static class Counters
    {
        private static string _version = string.Empty;
        private static string _serviceName = "Link Notification Service";
        private static string NotificationCreatedCounter = "notification-created";
        private static string NotificationSentCounter = "notification-sent";

        public static Meter meter = new Meter(_serviceName, _version);
        public static Counter<int> NotificationCreated = meter.CreateCounter<int>(NotificationCreatedCounter);
        public static Counter<int> NotificationSent = meter.CreateCounter<int>(NotificationSentCounter);
        public static void Initialize(ServiceInformation serviceInfo)
        {
            _serviceName = serviceInfo.Name;
            _version = serviceInfo.Version;

            //re-initialize meter
            Dispose();
            meter = new Meter(_serviceName, _version);
            NotificationCreated = meter.CreateCounter<int>(NotificationCreatedCounter);
            NotificationSent = meter.CreateCounter<int>(NotificationSentCounter);
        }

        public static void Dispose()
        {
            meter.Dispose();
        }

    }
}
