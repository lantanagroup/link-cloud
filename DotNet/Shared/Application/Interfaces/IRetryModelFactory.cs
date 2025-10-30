using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;

namespace LantanaGroup.Link.Shared.Application.Interfaces
{
    public interface IRetryModelFactory
    {
<<<<<<< Updated upstream
        RetryModel CreateRetryEntity(ConsumeResult<string, string> consumeResult, ConsumerSettings consumerSettings);
=======
        RetryEntity CreateRetryModel(ConsumeResult<string, string> consumeResult, ConsumerSettings consumerSettings);
>>>>>>> Stashed changes
    }
}
