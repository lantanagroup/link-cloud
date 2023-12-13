using Confluent.Kafka;
using LantanaGroup.Link.MeasureEval.Models;

namespace LantanaGroup.Link.MeasureEval.Auditing
{
    public interface IKafkaConsumerFactory
    {
        public IConsumer<string, NotificationMessage> CreateNotificationRequestedConsumer();
    }
}

