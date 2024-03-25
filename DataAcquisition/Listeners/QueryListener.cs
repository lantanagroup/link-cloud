using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Audit;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Census;
using LantanaGroup.Link.DataAcquisition.Application.Commands.PatientResource;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Application.Settings;
using LantanaGroup.Link.DataAcquisition.Entities;
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

    private readonly ILogger<QueryListener> _logger;
    private readonly IMediator _mediator;

    public QueryListener(
        ILogger<QueryListener> logger,
        IMediator mediator,
        IKafkaConsumerFactory<string, string> kafkaConsumerFactory,
        IKafkaProducerFactory<string, object> kafkaProducerFactory,
        IDeadLetterExceptionHandler<string, string> deadLetterConsumerHandler)
    {
        _logger = logger;
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentNullException(nameof(kafkaConsumerFactory));
        _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
        _deadLetterConsumerHandler = deadLetterConsumerHandler ?? throw new ArgumentNullException(nameof(deadLetterConsumerHandler));
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await System.Threading.Tasks.Task.Run(() => StartConsumerLoop(cancellationToken), cancellationToken);
    }

    private async System.Threading.Tasks.Task StartConsumerLoop(CancellationToken cancellationToken)
    {
        var settings = new ConsumerConfig
        {
            EnableAutoCommit = false,
            GroupId = "DataAcquisition-Query"
        };
        settings.GroupId = "DataAcquisitionGroup";
        settings.EnableAutoCommit = false;

        using var consumer = _kafkaConsumerFactory.CreateConsumer(settings);

        List<IBaseMessage>? responseMessages = new List<IBaseMessage>();
        consumer.Subscribe(new string[] { nameof(KafkaTopic.PatientCensusScheduled), nameof(KafkaTopic.DataAcquisitionRequested) });
        ConsumeResult<string, string> rawmessage = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                rawmessage = consumer.Consume(cancellationToken);
            }
            catch (Exception ex)
            {
                _deadLetterConsumerHandler.HandleException(rawmessage, ex, "");
                continue;
            }

            IBaseMessage deserializedMessage = null;
            (string facilityId, string correlationId) messageMetaData = (string.Empty, string.Empty);

            if (rawmessage != null)
            {
                IBaseMessage? message = null;
                try
                {
                    message = MessageDeserializer.DeserializeMessage(rawmessage.Topic, rawmessage.Message.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing message: {1}", ex.Message);
                    _deadLetterConsumerHandler.HandleException(rawmessage, ex, "");
                    continue;
                }

                deserializedMessage = message;

                try
                {
                    messageMetaData = ExtractFacilityIdAndCorrelationIdFromMessage(rawmessage.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error extracting facility id and correlation id from message");
                    _deadLetterConsumerHandler.HandleException(rawmessage, ex, "");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(messageMetaData.facilityId))
                {
                    var errorMessage = "No Facility ID provided. Unable to process message: {1}";
                    _logger.LogWarning(errorMessage, message);
                    _deadLetterConsumerHandler.HandleException(rawmessage, new Exception($"No Facility ID provided. Unable to process message: {message}"), "");
                    continue;
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
                catch (Exception ex)
                {
                    _deadLetterConsumerHandler.HandleException(rawmessage, ex, messageMetaData.facilityId);
                    _logger.LogError(ex, "Error producing message: {1}", ex.Message);
                    responseMessages = null;
                    continue;
                }

            }

            if (responseMessages?.Count > 0)
            {
                var producerSettings = new ProducerConfig();
                using var producer = _kafkaProducerFactory.CreateProducer(producerSettings);

                try
                {
                    if (rawmessage.Topic == KafkaTopic.PatientCensusScheduled.ToString())
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
                                Value = (PatientAcquiredMessage)responseMessage
                            };
                            await producer.ProduceAsync(KafkaTopic.PatientAcquired.ToString(), produceMessage, cancellationToken);

                            ProduceAuditMessage(new AuditEventMessage
                            {
                                CorrelationId = messageMetaData.correlationId,
                                FacilityId = messageMetaData.facilityId,
                                Action = AuditEventType.Query,
                                //Resource = string.Join(',', deserializedMessage.),
                                EventDate = DateTime.UtcNow,
                                ServiceName = DataAcquisitionConstants.ServiceName,
                                Notes = $"Raw Kafka Message: {rawmessage}\nRaw Message Produced: {JsonConvert.SerializeObject(responseMessage)}",
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _deadLetterConsumerHandler.HandleException(rawmessage, ex, messageMetaData.facilityId);
                    _logger.LogError(ex, "Failed to produce message");
                    continue;
                }
                consumer.Commit(rawmessage);
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
                    Notes = $"Message with topic: {rawmessage.Topic} meets no condition for processing. full message: {rawmessage.Message}",
                });
                _logger.LogWarning("Message with topic: {1} meets no condition for processing. full message: {2}", rawmessage.Topic, rawmessage.Message);
                _deadLetterConsumerHandler.HandleException(rawmessage, new Exception("Message meets no condition for processing"), messageMetaData.facilityId);
            }
        }
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