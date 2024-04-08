using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;

namespace LantanaGroup.Link.Shared.Application.Interfaces
{
    public interface IRetryEntityFactory
    {
        RetryEntity CreateRetryEntity(ConsumeResult<string, string> consumeResult, ConsumerSettings consumerSettings);
    }
}
