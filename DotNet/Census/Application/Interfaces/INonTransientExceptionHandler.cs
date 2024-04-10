using Confluent.Kafka;

namespace LantanaGroup.Link.Census.Application.Interfaces;

public interface INonTransientExceptionHandler<K, V>
{
    void HandleException(ConsumeException consumeException);
    void HandleException(ConsumeResult<K, V> consumeResult, Exception ex);
}
