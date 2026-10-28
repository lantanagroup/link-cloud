using Confluent.Kafka;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LantanaGroup.Link.Shared.Application.Interfaces;
public interface IConsumerLogic<ConsumeKeyType, ConsumeValueType, ProduceKeyType, ProduceValueType>
{
    Task executeLogic(ConsumeResult<ConsumeKeyType, ConsumeValueType> consumeResult, CancellationToken cancellationToken = default, params object[] optionalArgList);

    string extractFacilityId(ConsumeResult<ConsumeKeyType, ConsumeValueType> consumeResult);

    ConsumerConfig createConsumerConfig();
}
