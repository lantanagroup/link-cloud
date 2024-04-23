using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;
using System.Text;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Audit;

public class TriggerAuditEventCommand : IRequest<Unit>
{
    public AuditEventMessage AuditableEvent { get; set; }
}

public class TriggerAuditEventCommandHandler : IRequestHandler<TriggerAuditEventCommand, Unit>
{
    private readonly ILogger<TriggerAuditEventCommandHandler> _logger;
    //private readonly IKafkaWrapper<Ignore, Ignore, string, string> _auditKafkaWrapper;
    private readonly IKafkaProducerFactory<string, string> _kafkaProducerFactory;

    public TriggerAuditEventCommandHandler(ILogger<TriggerAuditEventCommandHandler> logger, IKafkaProducerFactory<string, string> kafkaProducerFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
    }

    public async Task<Unit> Handle(TriggerAuditEventCommand request, CancellationToken cancellationToken)
    {
        var headers = new Headers();
        headers.Add("X-Correlation-Id", Encoding.ASCII.GetBytes(request.AuditableEvent.CorrelationId != null ? request.AuditableEvent.CorrelationId : ""));

        var message = new Message<string, AuditEventMessage>
        {
            Key = request.AuditableEvent.FacilityId,
            Headers = headers,
            Value = request.AuditableEvent
        };

        await _kafkaProducerFactory.CreateAuditEventProducer(useOpenTelemetry: true).ProduceAsync(KafkaTopic.AuditableEventOccurred.ToString(), message, cancellationToken);
        return new Unit();
    }
}
