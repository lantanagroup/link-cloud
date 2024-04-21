using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Settings;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Notification.Infrastructure.Telemetry
{  
    public class NotificationServiceMetrics : INotificationServiceMetrics
    {
        public const string MeterName = $"Link.{NotificationConstants.ServiceName}";

        public NotificationServiceMetrics(IMeterFactory meterFactory)
        {
            Meter meter = meterFactory.Create(MeterName);
            NotificationCreatedCounter = meter.CreateCounter<int>("link_notification_service.notification_created.count");
            NotificationSentCounter = meter.CreateCounter<int>("link_notification_service.notification_sent.count");
        }

        public Counter<int> NotificationCreatedCounter { get; private set; }
        public void IncrementNotificationCreatedCounter(List<KeyValuePair<string, object?>> tags)
        {            
            NotificationCreatedCounter.Add(1, tags.ToArray());
        }

        public Counter<int> NotificationSentCounter { get; private set; }
        public void IncrementNotificationSentCounter(List<KeyValuePair<string, object?>> tags)
        {
            NotificationSentCounter.Add(1, tags.ToArray());
        }        
        
    }
}
