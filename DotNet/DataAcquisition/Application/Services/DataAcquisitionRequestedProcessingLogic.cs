using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Audit;
using LantanaGroup.Link.DataAcquisition.Application.Commands.PatientResource;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;
using Newtonsoft.Json;
using System.Text;

namespace LantanaGroup.Link.DataAcquisition.Application.Services;

public class DataAcquisitionRequestedProcessingLogic : IConsumerLogic<string, DataAcquisitionRequested, string, ResourceAcquired>
{
    private readonly ILogger<DataAcquisitionRequestedProcessingLogic> _logger;
    private readonly IMediator _mediator;
    private readonly IKafkaProducerFactory<string, ResourceAcquired> _kafkaProducerFactory;

    public DataAcquisitionRequestedProcessingLogic(
        ILogger<DataAcquisitionRequestedProcessingLogic> logger,
        IMediator mediator,
        IKafkaProducerFactory<string, ResourceAcquired> kafkaProducerFactory
        )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
    }

    public ConsumerConfig createConsumerConfig()
    {
        var settings = new ConsumerConfig
        {
            EnableAutoCommit = false,
            GroupId = ServiceActivitySource.ServiceName
        };
        return settings;
    }

    public async Task executeLogic(ConsumeResult<string, DataAcquisitionRequested> consumeResult, CancellationToken cancellationToken = default, params object[] optionalArgList)
    {
        string correlationId;
        string facilityId;

        try
        {
            correlationId = extractCorrelationId(consumeResult);
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex, "CorrelationId is missing from the message headers.");
            throw new DeadLetterException("CorrelationId is missing from the message headers.", Shared.Application.Models.AuditEventType.Query, ex);
        }

        try
        {
            facilityId = extractFacilityId(consumeResult);
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex, "FacilityId is missing from the message key.");
            throw new DeadLetterException("FacilityId is missing from the message key.", Shared.Application.Models.AuditEventType.Query, ex);
        }

        List<IBaseMessage> results = new List<IBaseMessage>();
        try
        {
            results = await _mediator.Send(new GetPatientDataRequest
            {
                Message = consumeResult.Message.Value,
                FacilityId = facilityId,
                CorrelationId = correlationId,
            }, cancellationToken);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            throw new TransientException("Facility configuration is missing: " + ex.Message, AuditEventType.Query, ex);
        }
        catch (FhirApiFetchFailureException ex)
        {
            throw new TransientException("Error fetching FHIR API: " + ex.Message, AuditEventType.Query, ex);
        }
        catch (Exception ex)
        {
            throw new TransientException("Error processing message: " + ex.Message, AuditEventType.Query, ex);
        }

        if (results?.Count > 0)
        {
            var producerSettings = new ProducerConfig();
            producerSettings.CompressionType = CompressionType.Zstd;

            using var producer = _kafkaProducerFactory.CreateProducer(producerSettings, useOpenTelemetry: true);

            try
            {
                foreach (var responseMessage in results)
                {
                    var headers = new Headers
                    {
                        new Header(DataAcquisitionConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(correlationId))
                    };
                    var produceMessage = new Message<string, ResourceAcquired>
                    {
                        Key = facilityId,
                        Headers = headers,
                        Value = (ResourceAcquired)responseMessage
                    };
                    await producer.ProduceAsync(KafkaTopic.ResourceAcquired.ToString(), produceMessage, cancellationToken);

                    ProduceAuditMessage(new AuditEventMessage
                    {
                        CorrelationId = correlationId,
                        FacilityId = facilityId,
                        Action = AuditEventType.Query,
                        //Resource = string.Join(',', deserializedMessage.),
                        EventDate = DateTime.UtcNow,
                        ServiceName = DataAcquisitionConstants.ServiceName,
                        Notes = $"Raw Kafka Message: {consumeResult}\nRaw Message Produced: {JsonConvert.SerializeObject(responseMessage)}",
                    });
                }
            }
            catch (ProduceException<string, object> ex)
            {
                throw new TransientException($"Failed to produce message to {consumeResult.Topic} for {facilityId}", AuditEventType.Query, ex);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }

    public string extractFacilityId(ConsumeResult<string, DataAcquisitionRequested> consumeResult)
    {
        var facilityId = consumeResult.Message.Key;

        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentNullException("FacilityId is missing from the message key.");

        return facilityId;
    }

    private string extractCorrelationId(ConsumeResult<string, DataAcquisitionRequested> consumeResult)
    {
        var cIBytes = consumeResult.Headers.FirstOrDefault(x => x.Key.ToLower() == DataAcquisitionConstants.HeaderNames.CorrelationId.ToLower())?.GetValueBytes();

        if (cIBytes == null || cIBytes.Length == 0)
            throw new ArgumentNullException("CorrelationId is missing from the message headers.");


        var correlationId = Encoding.UTF8.GetString(cIBytes);
        return correlationId;
    }

    private void ProduceAuditMessage(AuditEventMessage auditEvent)
    {
        var request = new TriggerAuditEventCommand
        {
            AuditableEvent = auditEvent
        };
        _mediator.Send(request);
    }
}
