using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Notification.Infrastructure.Telemetry
{  
    public class NotificationServiceMetrics
    {
        public const string MeterName = "LinkNotificationService";

        public NotificationServiceMetrics(IMeterFactory meterFactory)
        {
            Meter meter = meterFactory.Create(MeterName);
            NotificationCreatedCounter = meter.CreateCounter<int>("link_notification_service.notification.created.count");
            NotificationSentCounter = meter.CreateCounter<int>("link_notification_service.notification.sent.count");
        }

        public Counter<int> NotificationCreatedCounter { get; private set; }
        public Counter<int> NotificationSentCounter { get; private set; }
    }
}
