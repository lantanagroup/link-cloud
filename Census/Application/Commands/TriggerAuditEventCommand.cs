using Confluent.Kafka;
using LantanaGroup.Link.Census.Models.Messages;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Wrappers;
using MediatR;
using System.Text;

namespace LantanaGroup.Link.Census.Application.Commands;

public class TriggerAuditEventCommand : IRequest
{
    public AuditEventMessage AuditableEvent { get; set; }
}

public class TriggerAuditEventCommandHandler : IRequestHandler<TriggerAuditEventCommand>
{
    private readonly ILogger<TriggerAuditEventCommandHandler> _logger;
    private readonly IKafkaProducerFactory<string, Ignore> _kafkaProducerFactory;

    public TriggerAuditEventCommandHandler(ILogger<TriggerAuditEventCommandHandler> logger, IKafkaProducerFactory<string, Ignore> kafkaProducerFactory)
    {
        _logger = logger;
        _kafkaProducerFactory = kafkaProducerFactory;
    }

    public async Task Handle(TriggerAuditEventCommand request, CancellationToken cancellationToken)
    {
        using var producer = _kafkaProducerFactory.CreateAuditEventProducer();
        var headers = new Headers();
        headers.Add("X-Correlation-Id", Encoding.ASCII.GetBytes(request.AuditableEvent.CorrelationId));

        await producer.ProduceAsync(Shared.Application.Models.KafkaTopic.AuditableEventOccurred.ToString(), new Message<string, AuditEventMessage>
        {
            Key = request.AuditableEvent.FacilityId,
            Headers = headers,
            Value = request.AuditableEvent
        });
    }
}
