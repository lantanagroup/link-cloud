using LantanaGroup.Link.Notification.Application.Interfaces;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Notification.Infrastructure.Telemetry
{  
    public class NotificationServiceMetrics : INotificationServiceMetrics
    {
        public const string MeterName = "LinkNotificationService";

        public NotificationServiceMetrics(IMeterFactory meterFactory)
        {
            Meter meter = meterFactory.Create(MeterName);
            NotificationCreatedCounter = meter.CreateCounter<int>("link_notification_service.notification.created.count");
            NotificationSentCounter = meter.CreateCounter<int>("link_notification_service.notification.sent.count");
        }

        public void IncrementNotificationCreatedCounter(List<KeyValuePair<string, object?>> tags)
        {            
            NotificationCreatedCounter.Add(1, tags.ToArray());
        }

        public void IncrementNotificationSentCounter(List<KeyValuePair<string, object?>> tags)
        {
            NotificationSentCounter.Add(1, tags.ToArray());
        }

        public Counter<int> NotificationCreatedCounter { get; private set; }
        public Counter<int> NotificationSentCounter { get; private set; }
    }
}
