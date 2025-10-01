using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Messages;
using LantanaGroup.Link.Census.Application.Services;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Data.SqlClient;
using System.Text;
using static Confluent.Kafka.ConfigPropertyNames;

namespace LantanaGroup.Link.Census.Listeners;

public class CensusListener : BackgroundService
{
    private readonly IKafkaConsumerFactory<string, PatientIDsAcquired> _kafkaConsumerFactory;
    private readonly IProducer<string, object> _kafkaProducer;
    private readonly ILogger<CensusListener> _logger;
    private readonly IDeadLetterExceptionHandler<string, PatientIDsAcquired> _nonTransientExceptionHandler;
    private readonly ITransientExceptionHandler<string, PatientIDsAcquired> _transientExceptionHandler;
    private readonly IServiceScopeFactory _scopeFactory;

    public CensusListener(
        ILogger<CensusListener> logger,
        IKafkaConsumerFactory<string, PatientIDsAcquired> kafkaConsumerFactory, 
        IProducer<string, object> kafkaProducer, 
        IDeadLetterExceptionHandler<string, PatientIDsAcquired> nonTransientExceptionHandler, 
        ITransientExceptionHandler<string, PatientIDsAcquired> transientExceptionHandler,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentNullException(nameof(kafkaConsumerFactory));
        _kafkaProducer = kafkaProducer ?? throw new ArgumentNullException(nameof(kafkaProducer));
        _nonTransientExceptionHandler = nonTransientExceptionHandler ?? throw new ArgumentNullException(nameof(nonTransientExceptionHandler));
        _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentNullException(nameof(transientExceptionHandler));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));


        _transientExceptionHandler.ServiceName = CensusConstants.ServiceName;
        _transientExceptionHandler.Topic = nameof(KafkaTopic.PatientIDsAcquired) + "-Retry";
        _nonTransientExceptionHandler.ServiceName = CensusConstants.ServiceName;
        _nonTransientExceptionHandler.Topic = nameof(KafkaTopic.PatientIDsAcquired) + "-Error";
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
            GroupId = CensusConstants.ServiceName,
            EnableAutoCommit = false
        };
        using var kafkaConsumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig);

        IEnumerable<BaseResponse>? responseMessages = null;
        kafkaConsumer.Subscribe(KafkaTopic.PatientIDsAcquired.ToString());
        ConsumeResult<string, PatientIDsAcquired>? rawmessage = null;

        using var scope = _scopeFactory.CreateScope();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await kafkaConsumer.ConsumeWithInstrumentation((Func<ConsumeResult<string, PatientIDsAcquired>?, CancellationToken, System.Threading.Tasks.Task>)(async (result, CancellationToken) =>
                    {
                        rawmessage = result;

                        try
                        {
                            (string facilityId, string correlationId) messageMetaData = (string.Empty, string.Empty);

                            if (rawmessage != null)
                            {                              
                                if(rawmessage.Message.Value == null)
                                {
                                    throw new DeadLetterException("Message value is null", new MissingFacilityIdException("No message value provided. Unable to process message."));
                                }

                                var msgValue = rawmessage.Message.Value;

                                try
                                {
                                    messageMetaData = ExtractFacilityIdAndCorrelationIdFromMessage(rawmessage.Message);
                                    if (string.IsNullOrWhiteSpace(messageMetaData.facilityId))
                                    {
                                        throw new TransientException("Facility ID is null or empty", new MissingFacilityIdException("No Facility ID provided. Unable to process message."));
                                    }
                                }
                                catch (MissingFacilityIdException ex)
                                {
                                    throw new TransientException("Error extracting facility ID and correlation ID: " + ex.Message, ex);
                                }

                                try
                                {
                                    var patientIdsAcuiredService = scope.ServiceProvider.GetRequiredService<IPatientIdsAcquiredService>();
                                    var responseMessages = await patientIdsAcuiredService.ProcessEvent(new ConsumePatientIdsAcquiredEventModel()
                                    {
                                        FacilityId = messageMetaData.facilityId,
                                        Message = msgValue,
                                    }, cancellationToken);

                                    await ProduceEvents(responseMessages, cancellationToken);
                                }
                                catch(SqlException ex)
                                {
                                    throw new TransientException("DB Error processing message: " + ex.Message, ex);
                                }
                                catch (Exception ex)
                                {

                                    throw new DeadLetterException("Error processing message: " + ex.Message, ex);
                                }

                                kafkaConsumer.Commit(rawmessage);
                            }
                        }
                        catch (DeadLetterException ex)
                        {
                            _nonTransientExceptionHandler.Topic = rawmessage?.Topic + "-Error";
                            _nonTransientExceptionHandler.HandleException(rawmessage, ex, rawmessage.Key);
                            kafkaConsumer.Commit(rawmessage);
                        }
                        catch (TransientException ex)
                        {
                            _transientExceptionHandler.Topic = rawmessage?.Topic + "-Retry";
                            _transientExceptionHandler.HandleException(rawmessage, ex, rawmessage.Key);
                            kafkaConsumer.Commit(rawmessage);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to process Patient Event");

                            _nonTransientExceptionHandler.HandleException(rawmessage, ex, rawmessage.Message.Key);

                            kafkaConsumer.Commit(rawmessage);
                        }

                    }), cancellationToken);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message for topics: [{Topics}] at {Timestamp}", string.Join(", ", kafkaConsumer.Subscription), DateTime.UtcNow);

                    if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                    {
                        throw new OperationCanceledException(ex.Error.Reason, ex);
                    }

                    var facilityId = ex.ConsumerRecord.Message.Key != null ? Encoding.UTF8.GetString(ex.ConsumerRecord.Message.Key) : "";

                    _nonTransientExceptionHandler.HandleConsumeException(ex, facilityId);

                    var offset = ex.ConsumerRecord?.TopicPartitionOffset;
                    kafkaConsumer.Commit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error consuming message for topics: [{Topics}] at {Timestamp}", string.Join(", ", kafkaConsumer.Subscription), DateTime.UtcNow);
                    kafkaConsumer.Commit();
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation("Stopped census consumer for topic '{Topic}' at {DateTime}", KafkaTopic.PatientIDsAcquired, DateTime.UtcNow);
            kafkaConsumer.Close();
            kafkaConsumer.Dispose();
        }
    }

    private async System.Threading.Tasks.Task ProduceEvents(IEnumerable<BaseResponse> events, CancellationToken cancellationToken = default)
    {
        foreach (var ev in events)
        {
            if (ev.TopicName == KafkaTopic.PatientEvent.ToString())
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

                await _kafkaProducer.ProduceAsync(KafkaTopic.PatientEvent.ToString(), message);
            }
        }
    }

    private (string facilityId, string correlationId) ExtractFacilityIdAndCorrelationIdFromMessage(Message<string, PatientIDsAcquired> message)
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
