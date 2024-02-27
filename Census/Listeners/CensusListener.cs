using Census.Settings;
using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Commands;
using LantanaGroup.Link.Census.Models;
using LantanaGroup.Link.Census.Models.Messages;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;
using Newtonsoft.Json;
using System.Text;

namespace LantanaGroup.Link.Census.Listeners;

public class CensusListener : BackgroundService
{
    private readonly IKafkaConsumerFactory<string,string> _kafkaConsumerFactory;
    private readonly IKafkaProducerFactory<string,object> _kafkaProducerFactory;
    private readonly ILogger<CensusListener> _logger;
    private readonly IMediator _mediator;
    private readonly INonTransientExceptionHandler<string, string> _nonTransientExceptionHandler;

    public CensusListener(ILogger<CensusListener> logger, IMediator mediator, IKafkaConsumerFactory<string, string> kafkaConsumerFactory, IKafkaProducerFactory<string, object> kafkaProducerFactory, INonTransientExceptionHandler<string, string> nonTransientExceptionHandler)
    {
        _logger = logger;
        _mediator = mediator;
        _kafkaConsumerFactory = kafkaConsumerFactory;
        _kafkaProducerFactory = kafkaProducerFactory;
        _nonTransientExceptionHandler = nonTransientExceptionHandler;
    }

    public override async System.Threading.Tasks.Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);
    }


    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await System.Threading.Tasks.Task.Run(() => StartConsumerLoop(cancellationToken), cancellationToken);
    }

    private async System.Threading.Tasks.Task StartConsumerLoop(CancellationToken cancellationToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            GroupId = "CensusService-PatientIdsAcquired",
            EnableAutoCommit = false
        };
        using var kafkaConsumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig);

        var producerConfig = new ProducerConfig();
        using var kafkaProducer = _kafkaProducerFactory.CreateProducer(producerConfig);

        IEnumerable<BaseResponse>? responseMessages = null;
        kafkaConsumer.Subscribe(KafkaTopic.PatientIDsAcquired.ToString());
        ConsumeResult<string, string> rawmessage = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    rawmessage = kafkaConsumer.Consume(cancellationToken);
                }
                catch(ConsumeException ex)
                {
                    _nonTransientExceptionHandler.HandleException(ex);
                    kafkaConsumer.Commit(rawmessage);
                    _logger.LogError($"Error consuming message: {ex.Error.Reason}");
                    continue;
                }

                IBaseMessage deserializedMessage = null;
                (string facilityId, string correlationId) messageMetaData = (string.Empty, string.Empty);

                if (rawmessage != null)
                {
                    IBaseMessage message = null;
                    try
                    {
                        message = DeserializeMessage(rawmessage.Topic, rawmessage.Message.Value);
                    }
                    catch (Exception ex)
                    {
                        _nonTransientExceptionHandler.HandleException(rawmessage,ex);
                        kafkaConsumer.Commit(rawmessage);
                        var errorMessage = $"Unable to deserialize message: {rawmessage?.Message?.Value}";
                        _logger.LogError(errorMessage);
                        continue;
                    }

                    deserializedMessage = message;
                    messageMetaData = ExtractFacilityIdAndCorrelationIdFromMessage(rawmessage.Message);


                    try
                    {
                        if (string.IsNullOrWhiteSpace(messageMetaData.facilityId))
                        {
                            throw new MissingFacilityIdException("No Facility ID provided. Unable to process message.");
                        }
                    }
                    catch (MissingFacilityIdException ex)
                    {
                        _nonTransientExceptionHandler.HandleException(rawmessage, ex);
                        kafkaConsumer.Commit(rawmessage);
                        var errorMessage = $"No Facility ID provided. Unable to process message: {message}";
                        _logger.LogWarning(errorMessage);
                        continue;
                    }

                    try
                    {
                        responseMessages = message switch
                        {
                            PatientIDsAcquired => await _mediator.Send(new ConsumePatientIdsAcquiredEventCommand
                            {
                                FacilityId = messageMetaData.facilityId,
                                Message = (PatientIDsAcquired)message
                            }, cancellationToken),
                            _ => null
                        };

                        await ProduceEvents(responseMessages, kafkaProducer, cancellationToken);
                    }
                    catch (Exception ex)
                    {

                        await ProduceAuditMessage(null, messageMetaData.facilityId, new AuditEventMessage
                        {
                            Action = Shared.Application.Models.AuditEventType.Query,
                            //Resource = string.Join(',', deserializedMessage.Type),
                            ServiceName = CensusConstants.ServiceName,
                            Notes = $"Failed to get {rawmessage.Topic}\nException Message: {ex}\nRaw Message: {JsonConvert.SerializeObject(rawmessage)}",
                        });
                        _logger.LogError($"Failed to produce {KafkaTopic.DataAcquired.ToString()} message: {responseMessages}");
                        responseMessages = null;
                    }

                    kafkaConsumer.Commit(rawmessage);
                }
            }
        }
        catch(OperationCanceledException ex) 
        {
            _logger.LogInformation($"Stopped census consumer for topic '{KafkaTopic.PatientIDsAcquired}' at {DateTime.UtcNow}");
            kafkaConsumer.Close();
            kafkaConsumer.Dispose();
        }
    }

    private async System.Threading.Tasks.Task ProduceEvents(IEnumerable<BaseResponse> events, IProducer<string, object> producer, CancellationToken cancellationToken = default)
    {
        foreach(var ev in events)
        {
            if(ev.TopicName == KafkaTopic.PatientEvent.ToString())
            {
                if (((PatientEventResponse)ev).PatientEvent == null) return;
                PatientEvent? patientEvent = ((PatientEventResponse)ev).PatientEvent;

                Headers? headers = null;
                if (ev.CorrelationId != null)
                    headers = new Headers
                        {
                            new Header(CensusConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(ev.CorrelationId))
                        };
                var message = new Message<string, object>
                {
                    Key = ev.FacilityId,
                    Headers = headers ?? null,
                    Value = patientEvent
                };

                await producer.ProduceAsync(KafkaTopic.PatientEvent.ToString(), message);
            }
        }
    }

    private async System.Threading.Tasks.Task ProduceAuditMessage(string correlationId, string key, AuditEventMessage auditEvent)
    {

        using var auditProducer = _kafkaProducerFactory.CreateAuditEventProducer();

        Headers? headers = null;
        
        try
        {
            await auditProducer.ProduceAsync(KafkaTopic.AuditableEventOccurred.ToString(), new Message<string, Shared.Application.Models.Kafka.AuditEventMessage>
            {
                Key = key,
                Headers = headers ?? null,
                Value = new Shared.Application.Models.Kafka.AuditEventMessage
                {
                    Action = auditEvent.Action,
                    CorrelationId = correlationId,
                    ServiceName = CensusConstants.ServiceName,
                    EventDate = auditEvent.EventDate,
                    FacilityId = key,
                    Notes = auditEvent.Notes,
                    PropertyChanges = auditEvent.PropertyChanges,
                    Resource = auditEvent.Resource
                }
            });
        } 
        catch(Exception ex)
        {
            _logger.LogError("There was an issue sending an audit.", ex);
        }
        
    }

    private (string facilityId, string correlationId) ExtractFacilityIdAndCorrelationIdFromMessage(Message<string, string> message)
    {
        var facilityId = message.Key;
        var cIBytes = message.Headers.FirstOrDefault(x => x.Key == CensusConstants.HeaderNames.CorrelationId)?.GetValueBytes();

        if (cIBytes == null || cIBytes.Length == 0)
            return (facilityId, string.Empty);


        var correlationId = Encoding.UTF8.GetString(cIBytes);

        return (facilityId, correlationId);
    }

    private IBaseMessage DeserializeMessage(string topic, string rawMessage) =>
        topic switch
        {
            CensusConstants.MessageNames.PatientIDsAcquired => DeserializePatientIdsAcquired(rawMessage),
            _ => throw new Exception($"{topic} not a valid topic. Unable to deserialize message.")
        };

    private PatientIDsAcquired DeserializePatientIdsAcquired(string jsonContent)
    {
        var patientIdsAcquiredMessage = new PatientIDsAcquired();
        jsonContent = jsonContent.Replace("\t", string.Empty);
        jsonContent = jsonContent.Replace("\n", string.Empty);
        //var jsonDoc = System.Text.Json.JsonSerializer.
        byte[] byteArray = Encoding.UTF8.GetBytes(jsonContent);
        //byte[] byteArray = Encoding.ASCII.GetBytes(contents);
        MemoryStream stream = new MemoryStream(byteArray);
        var doc = System.Text.Json.JsonDocument.Parse(stream);
        doc.RootElement.TryGetProperty("PatientIds", out var patientids);
        var patientidsStr = patientids.ToString();
        var fhirList = new FhirJsonParser().Parse<List>(patientidsStr);
        return new PatientIDsAcquired 
        {
            PatientIds = fhirList
        };
    }
}
