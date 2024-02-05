using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Notification.Infrastructure.Telemetry
{  
    public class NotificationServiceMetrics
    {
        public NotificationServiceMetrics(IMeterFactory meterFactory)
        {
            Meter meter = meterFactory.Create("LinkNotificationService");
            NotificationCreatedCounter = meter.CreateCounter<int>("notification.created.count");
            NotificationSentCounter = meter.CreateCounter<int>("notification.sent.count");
        }

        public Counter<int> NotificationCreatedCounter { get; private set; }
        public Counter<int> NotificationSentCounter { get; private set; }
    }
}
