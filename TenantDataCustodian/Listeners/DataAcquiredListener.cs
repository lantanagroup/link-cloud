using Confluent.Kafka;
using LantanaGroup.Link.Shared.Models;
using LantanaGroup.Link.Shared.Wrappers;
using MediatR;
using TenantDataCustodian.Models;

namespace TenantDataCustodian.Listeners;

public class DataAcquiredListener : BackgroundService
{
    private readonly IKafkaWrapper<Ignore, DataAcquiredMessage, Null, Null> _kafkaWrapper;
    private readonly ILogger<DataAcquiredListener> _logger;
    private readonly IMediator _mediator;
    private bool _cancelled = false;

    public DataAcquiredListener(
        IKafkaWrapper<Ignore, DataAcquiredMessage, Null, Null> kafkaWrapper, 
        ILogger<DataAcquiredListener> logger, 
        IMediator mediator 
        )
    {
        _kafkaWrapper = kafkaWrapper;
        _logger = logger;
        _mediator = mediator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _kafkaWrapper.SubscribeToKafkaTopic(new string[] { KafkaTopic.DataAcquired.ToString() });

        while (!this._cancelled)
        {
            var message = _kafkaWrapper.ConsumeKafkaMessageWithTopic();
        }
    }
}
