using Confluent.Kafka;

namespace LantanaGroup.Link.DataAcquisition.Application.Interfaces;

public interface IConsumerLogic<ConsumeKeyType, ConsumeValueType, ProduceKeyType, ProduceValueType>
{
    Task executeLogic(ConsumeResult<ConsumeKeyType, ConsumeValueType> consumeResult, CancellationToken cancellationToken = default, params object[] optionalArgList);

    string extractFacilityId(ConsumeResult<ConsumeKeyType, ConsumeValueType> consumeResult);

    ConsumerConfig createConsumerConfig();
}
