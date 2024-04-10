using Confluent.Kafka;

namespace LantanaGroup.Link.Census.Application.Interfaces;

public interface ITransientExceptionHandler<K, V>
{
    void HandleException(ConsumeResult<K, V> consumeResult, Exception ex);
}
