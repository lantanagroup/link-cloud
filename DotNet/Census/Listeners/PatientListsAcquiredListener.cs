using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Messages;
using LantanaGroup.Link.Census.Application.Services;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.Data.SqlClient;
using System.Text;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Listeners;

public class PatientListsAcquiredListener : BackgroundService
{
    private readonly IKafkaConsumerFactory<string, PatientListMessage> _kafkaConsumerFactory;
    private readonly ILogger<PatientListsAcquiredListener> _logger;
    private readonly IDeadLetterExceptionHandler<string, PatientListMessage> _nonTransientExceptionHandler;
    private readonly ITransientExceptionHandler<string, PatientListMessage> _transientExceptionHandler;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventProducerService<PatientEvent> _eventProducerService;

    public PatientListsAcquiredListener(
        ILogger<PatientListsAcquiredListener> logger,
        IKafkaConsumerFactory<string, PatientListMessage> kafkaConsumerFactory,
        IProducer<string, object> kafkaProducer,
        IDeadLetterExceptionHandler<string, PatientListMessage> nonTransientExceptionHandler,
        ITransientExceptionHandler<string, PatientListMessage> transientExceptionHandler,
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


        _transientExceptionHandler.ServiceName = CensusConstants.ServiceName;
        _transientExceptionHandler.Topic = nameof(KafkaTopic.PatientListsAcquired) + "-Retry";
        _nonTransientExceptionHandler.ServiceName = CensusConstants.ServiceName;
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
        using var kafkaConsumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig);

        IEnumerable<IBaseResponse>? responseMessages = null;
        kafkaConsumer.Subscribe(KafkaTopic.PatientListsAcquired.ToString());
        ConsumeResult<string, PatientListMessage>? rawmessage = null;

        using var scope = _scopeFactory.CreateScope();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await kafkaConsumer.ConsumeWithInstrumentation((Func<ConsumeResult<string, PatientListMessage>?, CancellationToken, Task>)(async (result, CancellationToken) =>
                    {
                        rawmessage = result;
                        
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
                                        return;
                                    }
                                }

                                var facilityId = rawmessage.Key ?? throw new DeadLetterException("FacilityId is null.", new MissingFacilityIdException("No Facility ID provided. Unable to process message."));
                                
                                if (rawmessage.Message.Value == null)
                                {
                                    throw new DeadLetterException("Message value is null", new Exception("No message value provided. Unable to process message."));
                                }

                                var msgValue = rawmessage.Message.Value;

                                try
                                {
                                    var patientListService = scope.ServiceProvider.GetRequiredService<IPatientListService>();
                                    responseMessages = await patientListService.ProcessLists(facilityId, rawmessage.Message.Value.PatientLists, cancellationToken);

                                    // Inject reportTrackingId into each PatientEvent
                                    responseMessages = responseMessages.Select(resp =>
                                    {
                                        if (resp is PatientEventResponse per && per.PatientEvent != null)
                                        {
                                            per.PatientEvent.ReportTrackingId = rawmessage.Message.Value.ReportTrackingId;
                                        }
                                        return resp;
                                    }).ToList();


                                    if (responseMessages == null || !responseMessages.Any())
                                    {
                                        _logger.LogWarning("No response messages returned for facility {FacilityId}.", facilityId);
                                    }
                                    else
                                        await _eventProducerService.ProduceEventsAsync(facilityId, responseMessages, cancellationToken);
                                }
                                catch(SqlException ex)
                                {
                                    throw new TransientException("DB Error processing message: " + ex.Message, ex);
                                }
                                //add produce exeption catch
                                catch (ProduceException<string, List<PatientListItem>> ex)
                                {
                                    throw new TransientException("Error producing message: " + ex.Message, ex);
                                }
                                catch (Exception ex)
                                {

                                    throw new DeadLetterException("Error processing message: " + ex.Message, ex);
                                }
                            }
                        }
                        catch (DeadLetterException ex)
                        {
                            _nonTransientExceptionHandler.Topic = rawmessage?.Topic + "-Error";
                            _nonTransientExceptionHandler.HandleException(rawmessage, ex, rawmessage.Key);
                        }
                        catch (TransientException ex)
                        {
                            _transientExceptionHandler.Topic = rawmessage?.Topic + "-Retry";
                            _transientExceptionHandler.HandleException(rawmessage, ex, rawmessage.Key);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Failed to process Patient Event.");
                            _nonTransientExceptionHandler.HandleException(rawmessage, ex, rawmessage.Message.Key);
                        }
                        finally
                        {
                            kafkaConsumer.Commit(rawmessage);
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

                    var facilityId = ex.ConsumerRecord.Message.Key != null ? Encoding.UTF8.GetString(ex.ConsumerRecord.Message.Key) : "";

                    _nonTransientExceptionHandler.HandleConsumeException(ex, facilityId);

                    var offset = ex.ConsumerRecord?.TopicPartitionOffset;
                    kafkaConsumer.Commit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error consuming message for topics: [{subs}] at {dateTime}", string.Join(", ", kafkaConsumer.Subscription), DateTime.UtcNow);
                    kafkaConsumer.Commit();
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation("Stopped census consumer for topic '{topic}' at {dateTime}", KafkaTopic.PatientListsAcquired, DateTime.UtcNow );
            kafkaConsumer.Close();
            kafkaConsumer.Dispose();
        }
    } 
}
