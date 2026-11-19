using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Messages;
using LantanaGroup.Link.Census.Application.Services;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.Data.SqlClient;
using System.Text;
using System.Text.Json;

namespace LantanaGroup.Link.Census.Listeners;

public class PatientListsAcquiredListener : BackgroundService
{
    private readonly IKafkaConsumerFactory<string, PatientListMessage> _kafkaConsumerFactory;
    private readonly ILogger<PatientListsAcquiredListener> _logger;
    private readonly IDeadLetterExceptionHandler<PatientListsAcquiredListener, string, PatientListMessage> _nonTransientExceptionHandler;
    private readonly ITransientExceptionHandler<PatientListsAcquiredListener, string, PatientListMessage> _transientExceptionHandler;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventProducerService<PatientEvent> _eventProducerService;

    public PatientListsAcquiredListener(
        ILogger<PatientListsAcquiredListener> logger,
        IKafkaConsumerFactory<string, PatientListMessage> kafkaConsumerFactory,
        IProducer<string, object> kafkaProducer,
        IDeadLetterExceptionHandler<PatientListsAcquiredListener, string, PatientListMessage> nonTransientExceptionHandler,
        ITransientExceptionHandler<PatientListsAcquiredListener, string, PatientListMessage> transientExceptionHandler,
        IServiceScopeFactory scopeFactory,
        IEventProducerService<PatientEvent> eventProducerService
        )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentNullException(nameof(kafkaConsumerFactory));
        _eventProducerService = eventProducerService ?? throw new ArgumentNullException(nameof(eventProducerService));
        _nonTransientExceptionHandler = nonTransientExceptionHandler ?? throw new ArgumentNullException(nameof(nonTransientExceptionHandler));
        _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentNullException(nameof(transientExceptionHandler));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

        _transientExceptionHandler.Topic = nameof(KafkaTopic.PatientListsAcquired) + "-Retry";
        _nonTransientExceptionHandler.Topic = nameof(KafkaTopic.PatientListsAcquired) + "-Error";
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
        var consumerConfig = new ConsumerConfig
        {
            GroupId = CensusConstants.ServiceName,
            EnableAutoCommit = false
        };
        var assignmentTracker = new KafkaAssignmentTracker();
        using var kafkaConsumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig, assignmentTracker: assignmentTracker);

        IEnumerable<IBaseResponse>? responseMessages = null;
        kafkaConsumer.Subscribe(KafkaTopicNames.Subscription(
            KafkaTopic.PatientListsAcquired.ToString(),
            CensusConstants.ServiceName));
        ConsumeResult<string, PatientListMessage>? rawmessage = null;

        using var scope = _scopeFactory.CreateScope();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await kafkaConsumer.ConsumeWithInstrumentation((Func<ConsumeResult<string, PatientListMessage>?, CancellationToken, Task>)(async (result, consumeCancellationToken) =>
                    {
                        rawmessage = result;
                        var accounted = false;

                        try
                        {
                            if (rawmessage != null)
                            {
                                //check raw message headers for 'X-Exception-Service', if it doesn't match what is in CensusConstants.ServiceName, then skip this message.
                                if (rawmessage.Message.Headers.TryGetLastBytes(KafkaConstants.HeaderConstants.ExceptionService, out var exceptionService))
                                {
                                    //If retry event is not from the exception service, disregard the retry event
                                    if (Encoding.UTF8.GetString(exceptionService) != CensusConstants.ServiceName)
                                    {
                                        _logger.LogWarning("({className}) is detecting that ({instanceServiceName}) is different from the service that produced the message ({messageServiceName}). Message will be disregarded.", nameof(PatientListsAcquiredListener), CensusConstants.ServiceName, Encoding.UTF8.GetString(exceptionService));
                                        accounted = true;
                                        return;
                                    }
                                }

                                if (rawmessage.Message.Value == null)
                                {
                                    throw new DeadLetterException("Message value is null", new Exception("No message value provided. Unable to process message."));
                                }

                                var facilityId = KafkaIdentity.Facility(rawmessage.Message.Value.FacilityId, rawmessage.Key);
                                if (string.IsNullOrWhiteSpace(facilityId))
                                {
                                    throw new DeadLetterException("FacilityId is null.", new MissingFacilityIdException("No Facility ID provided. Unable to process message."));
                                }

                                var msgValue = rawmessage.Message.Value;

                                try
                                {
                                    var patientListService = scope.ServiceProvider.GetRequiredService<IPatientListService>();
                                    responseMessages = await patientListService.ProcessLists(facilityId, rawmessage.Message.Value.PatientLists, consumeCancellationToken);

                                    // Inject reportTrackingId into each PatientEvent
                                    responseMessages = responseMessages.Select(resp =>
                                    {
                                        if (resp is PatientEventResponse per && per.PatientEvent != null)
                                        {
                                            if (rawmessage.Message.Value.PatientLists.Any(list => list.PatientIds.Contains(per.PatientEvent.PatientId)))
                                            {
                                                per.PatientEvent.ReportTrackingId = rawmessage.Message.Value.ReportTrackingId;
                                            }
                                        }
                                        return resp;
                                    }).ToList();


                                    if (responseMessages == null || !responseMessages.Any())
                                    {
                                        _logger.LogWarning("No response messages returned for facility {FacilityId}.", facilityId);
                                    }
                                    else
                                        await _eventProducerService.ProduceEventsAsync(facilityId, responseMessages, consumeCancellationToken);
                                }
                                catch (SqlException ex)
                                {
                                    throw new TransientException("DB Error processing message: " + ex.Message, ex);
                                }
                                catch (ProduceException<string, List<PatientListItem>> ex)
                                {
                                    throw new TransientException("Error producing message: " + ex.Message, ex);
                                }
                                catch (Exception ex)
                                {
                                    if (ex is DeadLetterException || ex is TransientException || ex is OperationCanceledException)
                                        throw;

                                    throw new TransientException("Error processing message: " + ex.Message, ex);
                                }

                                accounted = true;
                            }
                        }
                        catch (DeadLetterException ex)
                        {
                            if (!string.IsNullOrWhiteSpace(rawmessage?.Topic))
                            {
                                _nonTransientExceptionHandler.Topic = KafkaTopicNames.Error(KafkaTopicNames.Main(rawmessage.Topic));
                            }

                            accounted = false;
                            if (rawmessage != null)
                            {
                                accounted = await DeadLetterCommit.AccountAsync(
                                    _nonTransientExceptionHandler.HandleException(rawmessage, ex, FacilityIdOf(rawmessage.Message)),
                                    kafkaConsumer,
                                    rawmessage,
                                    _logger,
                                    consumeCancellationToken);
                            }
                        }
                        catch (TransientException ex)
                        {
                            if (!string.IsNullOrWhiteSpace(rawmessage?.Topic))
                            {
                                _transientExceptionHandler.Topic = KafkaTopicNames.Main(rawmessage.Topic) + "-Retry";
                            }
                            _transientExceptionHandler.HandleException(rawmessage, ex, FacilityIdOf(rawmessage?.Message));
                            accounted = true;
                        }
                        catch (OperationCanceledException) when (consumeCancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to process Patient Event.");
                            _transientExceptionHandler.HandleException(rawmessage, ex, FacilityIdOf(rawmessage?.Message));
                            accounted = true;
                        }
                        finally
                        {
                            if (accounted && rawmessage != null && !consumeCancellationToken.IsCancellationRequested)
                            {
                                assignmentTracker.MarkProcessed(rawmessage);
                                kafkaConsumer.SafeCommit(rawmessage, _logger);
                            }
                        }

                    }), cancellationToken);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message for topics: [{subscriptions}] at {dateTime}", string.Join(", ", kafkaConsumer.Subscription), DateTime.UtcNow);

                    if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                    {
                        throw new OperationCanceledException(ex.Error.Reason, ex);
                    }

                    var facilityId = FacilityIdFromConsumeException(ex);

                    _nonTransientExceptionHandler.HandleConsumeException(ex, facilityId);

                    var offset = ex.ConsumerRecord?.TopicPartitionOffset;
                    kafkaConsumer.SafeCommit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset }, _logger);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error consuming message for topics: [{subs}] at {dateTime}", string.Join(", ", kafkaConsumer.Subscription), DateTime.UtcNow);
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation("Stopped census consumer for topic '{topic}' at {dateTime}", KafkaTopic.PatientListsAcquired, DateTime.UtcNow);
            kafkaConsumer.Close();
            kafkaConsumer.Dispose();
        }
    }

    private static string FacilityIdOf(Message<string, PatientListMessage>? message) =>
        KafkaIdentity.Facility(message?.Value?.FacilityId, message?.Key) ?? string.Empty;

    private static string FacilityIdFromConsumeException(ConsumeException exception)
    {
        string? keyText = null;
        if (exception.ConsumerRecord?.Message?.Key is { Length: > 0 } keyBytes)
        {
            keyText = Encoding.UTF8.GetString(keyBytes);
        }

        string? valueFacilityId = null;
        if (exception.ConsumerRecord?.Message?.Value is { Length: > 0 } valueBytes)
        {
            try
            {
                var value = JsonSerializer.Deserialize<PatientListMessage>(valueBytes, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                });
                valueFacilityId = value?.FacilityId;
            }
            catch (JsonException)
            {
                // The value is not readable. The legacy key is the remaining source.
            }
        }

        return KafkaIdentity.Facility(valueFacilityId, keyText) ?? string.Empty;
    }
}
