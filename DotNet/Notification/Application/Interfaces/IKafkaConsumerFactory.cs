using Confluent.Kafka;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Notification.Application.Interfaces
{
    public interface IKafkaConsumerFactory
    {
        public IConsumer<string, NotificationMessage> CreateNotificationRequestedConsumer(bool enableAutoCommit, KafkaAssignmentTracker? assignmentTracker = null);
    }
}
