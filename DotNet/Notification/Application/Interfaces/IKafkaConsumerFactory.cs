using Confluent.Kafka;
using LantanaGroup.Link.Notification.Application.Models;

namespace LantanaGroup.Link.Notification.Application.Interfaces
{
    public interface IKafkaConsumerFactory
    {
        public IConsumer<string, NotificationMessage> CreateNotificationRequestedConsumer(bool enableAutoCommit);
    }
}
