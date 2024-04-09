using Confluent.Kafka;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Errors;

public class TransientPatientIDsAcquiredExceptionHandler<K,V>: ITransientExceptionHandler<K,V>
{
    private readonly ILogger<TransientPatientIDsAcquiredExceptionHandler<K,V>> _logger;
    private readonly IKafkaProducerFactory<string, AuditEventMessage> _auditProducerFactory;
    private readonly IKafkaProducerFactory<string, string> _patientIDsAcquiredProducerFactory;

    public TransientPatientIDsAcquiredExceptionHandler(ILogger<TransientPatientIDsAcquiredExceptionHandler<K, V>> logger, IKafkaProducerFactory<string, AuditEventMessage> auditProducerFactory, IKafkaProducerFactory<string, string> patientIDsAcquiredProducerFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditProducerFactory = auditProducerFactory ?? throw new ArgumentNullException(nameof(auditProducerFactory));
        _patientIDsAcquiredProducerFactory = patientIDsAcquiredProducerFactory ?? throw new ArgumentNullException(nameof(patientIDsAcquiredProducerFactory));
    }

    public void HandleException(ConsumeResult<K,V> consumeResult, Exception ex)
    {
        //No use case identified to warrant implementation. Once identified, implement.
        //example: https://github.com/lantanagroup/link-poc/blob/dispatch-error/QueryDispatch/Application/Errors/Handlers/TransientPatientEventExceptionHandler.cs
        throw new NotImplementedException();
    }
}

