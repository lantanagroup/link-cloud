using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Error.Exceptions;

namespace LantanaGroup.Link.Report.Application.Error.Interfaces
{
    public interface IReportExceptionHandler<K, V>
    {
        void HandleException(ConsumeResult<K, V> consumeResult, Exception ex);
    }
}
