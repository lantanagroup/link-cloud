using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Application.Interfaces;

public interface IConsumerCustomLogic<ConsumeKeyType, ConsumeValueType, ProduceKeyType, ProduceValueType>
{
    Task executeCustomLogic(ConsumeResult<ConsumeKeyType, ConsumeValueType> consumeResult, CancellationToken cancellationToken = default, params object[] optionalArgList);

    string extractFacilityId(ConsumeResult<ConsumeKeyType, ConsumeValueType> consumeResult);
}
