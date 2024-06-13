using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Audit;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Census;
using LantanaGroup.Link.DataAcquisition.Application.Commands.PatientResource;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Application.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;
using MongoDB.Driver;
using Newtonsoft.Json;
using System.Text;

namespace LantanaGroup.Link.DataAcquisition.Listeners;

public class QueryListener : BackgroundService
{

    private readonly IKafkaConsumerFactory<string, string> _kafkaConsumerFactory;
    private readonly IKafkaProducerFactory<string, object> _kafkaProducerFactory;

    private readonly IDeadLetterExceptionHandler<string, string> _deadLetterConsumerHandler;
    private readonly ITransientExceptionHandler<string, string> _transientExceptionHandler;

    private readonly ILogger<QueryListener> _logger;
    private readonly IMediator _mediator;

    public QueryListener(
        ILogger<QueryListener> logger,
        IMediator mediator,
        IKafkaConsumerFactory<string, string> kafkaConsumerFactory,
        IKafkaProducerFactory<string, object> kafkaProducerFactory,
        IDeadLetterExceptionHandler<string, string> deadLetterConsumerHandler,
        ITransientExceptionHandler<string, string> transientExceptionHandler)
    {
        _logger = logger;
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentNullException(nameof(kafkaConsumerFactory));
        _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
        _deadLetterConsumerHandler = deadLetterConsumerHandler ?? throw new ArgumentNullException(nameof(deadLetterConsumerHandler));
        _deadLetterConsumerHandler.ServiceName = DataAcquisitionConstants.ServiceName;
        _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentNullException(nameof(transientExceptionHandler));
        _transientExceptionHandler.ServiceName = DataAcquisitionConstants.ServiceName;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await Task.Run(() => StartConsumerLoop(cancellationToken), cancellationToken);
    }

    private async Task StartConsumerLoop(CancellationToken cancellationToken)
    {
        var settings = new ConsumerConfig
        {
            EnableAutoCommit = false,
            GroupId = DataAcquisitionConstants.ServiceName
        };

        using var consumer = _kafkaConsumerFactory.CreateConsumer(settings);

        try
        {
            List<IBaseMessage>? responseMessages = new List<IBaseMessage>();
            consumer.Subscribe(new string[] { nameof(KafkaTopic.PatientCensusScheduled), nameof(KafkaTopic.DataAcquisitionRequested) });
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await consumer.ConsumeWithInstrumentation(async (result, CancellationToken) =>
                    {
                        try
                        {
                            IBaseMessage deserializedMessage = null;
                            (string facilityId, string correlationId) messageMetaData = (string.Empty, string.Empty);

                            if (result != null)
                            {
                                IBaseMessage? message = null;
                                try
                                {
                                    message = MessageDeserializer.DeserializeMessage(result.Topic, result.Message.Value);
                                }
                                catch (Exception ex)
                                {
                                    throw new DeadLetterException("Error deserializing message: " + ex.Message, AuditEventType.Query, ex);
                                }

                                deserializedMessage = message;

                                try
                                {
                                    messageMetaData = ExtractFacilityIdAndCorrelationIdFromMessage(result.Message);
                                }
                                catch (Exception ex)
                                {
                                    throw new DeadLetterException("Error extracting facility ID and correlation ID: " + ex.Message, AuditEventType.Query, ex);
                                }

                                if (string.IsNullOrWhiteSpace(messageMetaData.facilityId))
                                {
                                    throw new DeadLetterException("Facility ID is null or empty", AuditEventType.Query);
                                }

                                try
                                {
                                    responseMessages = message switch
                                    {
                                        DataAcquisitionRequestedMessage => await _mediator.Send(new GetPatientDataRequest
                                        {
                                            Message = (DataAcquisitionRequestedMessage)message,
                                            FacilityId = messageMetaData.facilityId,
                                            CorrelationId = messageMetaData.correlationId,
                                        }, cancellationToken),
                                        PatientCensusScheduledMessage => await _mediator.Send(new GetPatientCensusRequest
                                        {
                                            FacilityId = messageMetaData.facilityId
                                        }, cancellationToken),
                                        _ => null
                                    };
                                }
                                catch(MissingFacilityConfigurationException ex)
                                {
                                    throw new TransientException("Facility configuration is missing: " + ex.Message, AuditEventType.Query, ex);
                                }
                                catch(FhirApiFetchFailureException ex)
                                {
                                    throw new TransientException("Error fetching FHIR API: " + ex.Message, AuditEventType.Query, ex);
                                }
                                catch (Exception ex)
                                {
                                    throw new TransientException("Error processing message: " + ex.Message, AuditEventType.Query, ex);
                                }
                            }

                            if (responseMessages?.Count > 0)
                            {
                                var producerSettings = new ProducerConfig();

                                if (result.Topic == KafkaTopic.DataAcquisitionRequested.ToString())
                                {
                                    producerSettings.CompressionType = CompressionType.Zstd;
                                }

                                using var producer = _kafkaProducerFactory.CreateProducer(producerSettings, useOpenTelemetry: true);

                                try
                                {
                                    if (result.Topic == KafkaTopic.PatientCensusScheduled.ToString())
                                    {
                                        var produceMessage = new Message<string, object>
                                        {
                                            Key = messageMetaData.facilityId,
                                            Value = (PatientIDsAcquiredMessage)responseMessages[0]
                                        };

                                        await producer.ProduceAsync(KafkaTopic.PatientIDsAcquired.ToString(), produceMessage, cancellationToken);
                                    }
                                    else
                                    {

                                        foreach (var responseMessage in responseMessages)
                                        {
                                            var headers = new Headers
                                            {
                                                new Header(DataAcquisitionConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(messageMetaData.correlationId))
                                            };
                                            var produceMessage = new Message<string, object>
                                            {
                                                Key = messageMetaData.facilityId,
                                                Headers = headers,
                                                Value = (ResourceAcquired)responseMessage
                                            };
                                            await producer.ProduceAsync(KafkaTopic.ResourceAcquired.ToString(), produceMessage, cancellationToken);

                                            ProduceAuditMessage(new AuditEventMessage
                                            {
                                                CorrelationId = messageMetaData.correlationId,
                                                FacilityId = messageMetaData.facilityId,
                                                Action = AuditEventType.Query,
                                                //Resource = string.Join(',', deserializedMessage.),
                                                EventDate = DateTime.UtcNow,
                                                ServiceName = DataAcquisitionConstants.ServiceName,
                                                Notes = $"Raw Kafka Message: {result}\nRaw Message Produced: {JsonConvert.SerializeObject(responseMessage)}",
                                            });
                                        }
                                    }
                                }
                                catch(ProduceException<string, object> ex)
                                {
                                    throw new TransientException($"Failed to produce message to {_transientExceptionHandler.Topic} for {messageMetaData.facilityId}", AuditEventType.Query, ex);
                                }
                                catch (Exception ex)
                                {
                                    _deadLetterConsumerHandler.Topic = result?.Topic + "-Error";
                                    _deadLetterConsumerHandler.HandleException(result, ex, AuditEventType.Query, messageMetaData.facilityId);
                                    _logger.LogError(ex, "Failed to produce message");
                                }
                                consumer.Commit(result);
                            }
                            else
                            {
                                ProduceAuditMessage(new AuditEventMessage
                                {
                                    CorrelationId = messageMetaData.correlationId,
                                    FacilityId = messageMetaData.facilityId,
                                    Action = AuditEventType.Query,
                                    //Resource = string.Join(',', deserializedMessage.Type),
                                    ServiceName = DataAcquisitionConstants.ServiceName,
                                    EventDate = DateTime.UtcNow,
                                    Notes = $"Message with topic: {result.Topic}. No messages were produced. Please check logs. full message: {result.Message}",
                                });
                                _logger.LogWarning("Message with topic: {1}. No messages were produced. Please check logs. full message: {2}", result.Topic, result.Message);

                                throw new DeadLetterException("No messages were produced. Please check logs.", AuditEventType.Query);
                            }
                        }
                        catch (DeadLetterException ex)
                        {
                            _deadLetterConsumerHandler.Topic = result?.Topic + "-Error";
                            _deadLetterConsumerHandler.HandleException(result, ex, result.Key);
                            consumer.Commit(result);
                        }
                        catch (TransientException ex)
                        {
                            _transientExceptionHandler.Topic = result?.Topic + "-Retry";
                            _transientExceptionHandler.HandleException(result, ex, result.Key);
                            consumer.Commit(result);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Failed to process Patient Event.");

                            _deadLetterConsumerHandler.HandleException(result, new DeadLetterException("Data Acquisition Exception thrown: " + ex.Message, AuditEventType.Create), result.Message.Key);
                            consumer.Commit(result);
                        }
                    }, cancellationToken);
                }
                catch (ConsumeException e)
                {
                    if (e.Error.Code == ErrorCode.UnknownTopicOrPart)
                    {
                        throw new OperationCanceledException(e.Error.Reason, e);
                    }

                    var facilityId = e.ConsumerRecord.Message.Key != null ? Encoding.UTF8.GetString(e.ConsumerRecord.Message.Key) : "";

                    var converted_record = new ConsumeResult<string, string>()
                    {
                        Message = new Message<string, string>()
                        {
                            Key = facilityId,
                            Value = e.ConsumerRecord.Message.Value != null ? Encoding.UTF8.GetString(e.ConsumerRecord.Message.Value) : "",
                            Headers = e.ConsumerRecord.Message.Headers
                        }
                    };

                    _deadLetterConsumerHandler.HandleException(converted_record, new DeadLetterException("Consume Result exception: " + e.InnerException.Message, AuditEventType.Create), facilityId);

                    consumer.Commit();
                    continue;
                }
            }
        }
        catch (OperationCanceledException oce)
        {
            _logger.LogError(oce, "Operation Canceled: {1}", oce.Message);
            consumer.Close();
            consumer.Dispose();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private void HandleDeadletterException(DeadLetterException ex)
    {

    }

    private void ProduceAuditMessage(AuditEventMessage auditEvent)
    {
        var request = new TriggerAuditEventCommand
        {
            AuditableEvent = auditEvent
        };
        _mediator.Send(request);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
    }

    private (string facilityId, string correlationId) ExtractFacilityIdAndCorrelationIdFromMessage(Message<string, string> message)
    {
        var facilityId = message.Key;
        var cIBytes = message.Headers.FirstOrDefault(x => x.Key.ToLower() == DataAcquisitionConstants.HeaderNames.CorrelationId.ToLower())?.GetValueBytes();

        if (cIBytes == null || cIBytes.Length == 0)
            return (facilityId, string.Empty);


        var correlationId = Encoding.UTF8.GetString(cIBytes);

        return (facilityId, correlationId);
    }
}