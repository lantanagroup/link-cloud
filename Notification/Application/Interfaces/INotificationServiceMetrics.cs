namespace LantanaGroup.Link.Notification.Application.Interfaces
{
    public interface INotificationServiceMetrics
    {
        void IncrementNotificationCreatedCounter(List<KeyValuePair<string, object?>> tags);
        void IncrementNotificationSentCounter(List<KeyValuePair<string, object?>> tags);
    }
}
