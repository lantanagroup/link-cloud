using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Census.Application.Models.Messages;
using LantanaGroup.Link.Census.Application.Services;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Census.Listeners;

/// <summary>
/// Consumes ADTAcquired events, keyed by facility so that a facility's events are applied in the order they were read.
/// </summary>
public class ADTAcquiredListener : BackgroundService
{
    private readonly IKafkaConsumerFactory<string, ADTAcquired> _kafkaConsumerFactory;
    private readonly ILogger<ADTAcquiredListener> _logger;
    private readonly IDeadLetterExceptionHandler<ADTAcquiredListener, string, ADTAcquired> _deadLetterExceptionHandler;
    private readonly ITransientExceptionHandler<ADTAcquiredListener, string, ADTAcquired> _transientExceptionHandler;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventProducerService<PatientEvent> _eventProducerService;

    private string ClassName => GetType().Name;

    public ADTAcquiredListener(
        IKafkaConsumerFactory<string, ADTAcquired> kafkaConsumerFactory,
        ILogger<ADTAcquiredListener> logger,
        IDeadLetterExceptionHandler<ADTAcquiredListener, string, ADTAcquired> deadLetterExceptionHandler,
        ITransientExceptionHandler<ADTAcquiredListener, string, ADTAcquired> transientExceptionHandler,
        IServiceScopeFactory scopeFactory,
        IEventProducerService<PatientEvent> eventProducerService)
    {
        _kafkaConsumerFactory = kafkaConsumerFactory;
        _logger = logger;
        _deadLetterExceptionHandler = deadLetterExceptionHandler;
        _transientExceptionHandler = transientExceptionHandler;
        _scopeFactory = scopeFactory;
        _eventProducerService = eventProducerService;

        _transientExceptionHandler.Topic = nameof(KafkaTopic.ADTAcquired) + "-Retry";
        _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.ADTAcquired) + "-Error";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
    }

    private async Task StartConsumerLoop(CancellationToken cancellationToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            GroupId = CensusConstants.ServiceName,
            EnableAutoCommit = false
        };

        using var consumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig);
        try
        {
            consumer.Subscribe(KafkaTopicNames.Subscription(
                nameof(KafkaTopic.ADTAcquired),
                CensusConstants.ServiceName));
            _logger.LogInformation("Started {Name} consumer on {Date} for topic '{TopicName}'", ClassName, DateTime.UtcNow, nameof(KafkaTopic.ADTAcquired));

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                    {
                        var facilityId = result.Message.Key;

                        try
                        {
                            await ProcessMessageAsync(result, consumeCancellationToken);
                        }
                        catch (DeadLetterException ex)
                        {
                            _deadLetterExceptionHandler.HandleException(result, ex, facilityId);
                        }
                        catch (TransientException ex)
                        {
                            _transientExceptionHandler.HandleException(result, ex, facilityId);
                        }
                        catch (OperationCanceledException) when (consumeCancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (OperationCanceledException ex)
                        {
                            _transientExceptionHandler.HandleException(result, new TransientException("Operation canceled (non-shutdown): " + ex.Message, ex), facilityId);
                        }
                        catch (Exception ex)
                        {
                            _deadLetterExceptionHandler.HandleException(result, new DeadLetterException(ClassName + " Exception thrown: " + ex.Message), facilityId);
                        }
                        finally
                        {
                            if (!consumeCancellationToken.IsCancellationRequested)
                            {
                                consumer.Commit(result);
                            }
                        }
                    }, cancellationToken);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message for topics: [{Topics}] at {Timestamp}", string.Join(", ", consumer.Subscription), DateTime.UtcNow);

                    if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                    {
                        throw new OperationCanceledException(ex.Error.Reason, ex);
                    }

                    _deadLetterExceptionHandler.HandleConsumeException(ex, string.Empty);

                    var offset = ex.ConsumerRecord?.TopicPartitionOffset;
                    consumer.Commit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset });
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error encountered in {Name}", ClassName);
                    consumer.Commit();
                }
            }
        }
        catch (OperationCanceledException oce)
        {
            _logger.LogError(oce, "Operation Canceled: {Message}", oce.Message);
            consumer.Close();
            consumer.Dispose();
        }
    }

    public async Task ProcessMessageAsync(ConsumeResult<string, ADTAcquired> result, CancellationToken cancellationToken)
    {
        if (result.Message.Value == null)
        {
            throw new DeadLetterException($"{ClassName}: ADTAcquired event value segment missing");
        }

        if (string.IsNullOrWhiteSpace(result.Message.Key))
        {
            throw new DeadLetterException($"{ClassName}: ADTAcquired event key (facility id) missing");
        }

        _logger.LogDebug("Consuming ADTAcquired event (Facility = {FacilityId})", result.Message.Key.SanitizeForLog());

        using var scope = _scopeFactory.CreateScope();
        var adtEventService = scope.ServiceProvider.GetRequiredService<IAdtEventService>();

        // The changes are rolled back on failure, and repeated messages are recognized if they were not
        try
        {
            var messages = await adtEventService.ProcessEvents(result.Message.Key, result.Message.Value, cancellationToken);
            await _eventProducerService.ProduceEventsAsync(result.Message.Key, messages, cancellationToken);
        }
        catch (Exception ex) when (ex is SqlException or DbUpdateException or TimeoutException or KafkaException)
        {
            throw new TransientException("Error processing ADTAcquired event: " + ex.Message, ex);
        }
    }
}
