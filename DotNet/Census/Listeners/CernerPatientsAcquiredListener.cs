
using Confluent.Kafka;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models;
using System.Text;
using System.Text.Json;
using System.Threading;
using LantanaGroup.Link.Census.Application.Services;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models.Messages;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace LantanaGroup.Link.Census.Listeners
{
    public class CernerPatientsAcquiredListener : BackgroundService
    {
        private readonly IKafkaConsumerFactory<string, CernerPatientsAcquired> _kafkaConsumerFactory;
        private readonly ILogger<PatientListsAcquiredListener> _logger;
        private readonly IDeadLetterExceptionHandler<CernerPatientsAcquiredListener, string, CernerPatientsAcquired> _deadLetterExceptionHandler;
        private readonly ITransientExceptionHandler<CernerPatientsAcquiredListener, string, CernerPatientsAcquired> _transientExceptionHandler;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IEventProducerService<PatientEvent> _eventProducerService;

        private string ClassName => this.GetType().Name;

        public CernerPatientsAcquiredListener(IKafkaConsumerFactory<string, CernerPatientsAcquired> kafkaConsumerFactory, ILogger<PatientListsAcquiredListener> logger, IDeadLetterExceptionHandler<CernerPatientsAcquiredListener, string, CernerPatientsAcquired> deadLetterExceptionHandler, ITransientExceptionHandler<CernerPatientsAcquiredListener, string, CernerPatientsAcquired> transientExceptionHandler, IServiceScopeFactory scopeFactory, IEventProducerService<PatientEvent> eventProducerService)
        {
            _kafkaConsumerFactory = kafkaConsumerFactory;
            _logger = logger;
            _deadLetterExceptionHandler = deadLetterExceptionHandler;
            _transientExceptionHandler = transientExceptionHandler;
            _scopeFactory = scopeFactory;
            _eventProducerService = eventProducerService;

            _transientExceptionHandler.Topic = nameof(KafkaTopic.CernerPatientsAcquired) + "-Retry";
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.CernerPatientsAcquired) + "-Error";
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await Task.Run(() => StartConsumerLoop(cancellationToken), cancellationToken);
        }

        private async Task StartConsumerLoop(CancellationToken cancellationToken)
        {
            var consumerConfig = new ConsumerConfig()
            {
                GroupId = CensusConstants.ServiceName,
                EnableAutoCommit = false
            };

            var assignmentTracker = new KafkaAssignmentTracker();
            using var consumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig, assignmentTracker: assignmentTracker);
            try
            {
                consumer.Subscribe(KafkaTopicNames.Subscription(
                    nameof(KafkaTopic.CernerPatientsAcquired),
                    CensusConstants.ServiceName));
                _logger.LogInformation("Started {name} consumer on {date} for topic '{TopicName}'", ClassName, DateTime.UtcNow, nameof(KafkaTopic.CernerPatientsAcquired));

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                        {
                            var accounted = false;
                            try
                            {
                                await ProcessMessageAsync(result, consumeCancellationToken);
                                accounted = true;
                            }
                            catch (DeadLetterException ex)
                            {
                                _deadLetterExceptionHandler.HandleException(result, ex, FacilityIdOf(result?.Message));
                                accounted = true;
                            }
                            catch (TransientException ex)
                            {
                                _transientExceptionHandler.HandleException(result, ex, FacilityIdOf(result?.Message));
                                accounted = true;
                            }
                            catch (OperationCanceledException) when (consumeCancellationToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (OperationCanceledException ex)
                            {
                                _transientExceptionHandler.HandleException(result, new TransientException("Operation canceled (non-shutdown): " + ex.Message, ex), FacilityIdOf(result?.Message));
                                accounted = true;
                            }
                            catch (Exception ex)
                            {
                                _deadLetterExceptionHandler.HandleException(result, new DeadLetterException(ClassName + " Exception thrown: " + ex.Message), FacilityIdOf(result?.Message));
                                accounted = true;
                            }
                            finally
                            {
                                if (accounted && result != null && !consumeCancellationToken.IsCancellationRequested)
                                {
                                    assignmentTracker.MarkProcessed(result);
                                    consumer.SafeCommit(result, _logger);
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

                        var facilityId = FacilityIdFromConsumeException(ex);
                        _deadLetterExceptionHandler.HandleConsumeException(ex, facilityId);

                        var offset = ex.ConsumerRecord?.TopicPartitionOffset;
                        consumer.SafeCommit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset }, _logger);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error encountered in CernerPatientsAcquiredListener");
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

        public async Task ProcessMessageAsync(ConsumeResult<string, CernerPatientsAcquired> result, CancellationToken cancellationToken)
        {
            if (result.Message.Value == null)
            {
                throw new DeadLetterException($"{ClassName}: CernerPatientsAcquired event value segment missing");
            }

            var facilityId = KafkaIdentity.Facility(result.Message.Value.FacilityId, result.Message.Key);
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                throw new DeadLetterException($"{ClassName}: Facility id is missing from the message.");
            }

            _logger.LogDebug("Consuming Event (Facility = {FacilityId})", facilityId);

            using var scope = _scopeFactory.CreateScope();
            var cernerListService = scope.ServiceProvider.GetRequiredService<ICernerListService>();

            var dischargeEvents = await cernerListService.ProcessDischarges(facilityId, result.Message.Value, cancellationToken);

            if (dischargeEvents != null)
            {
                await _eventProducerService.ProduceEventsAsync(facilityId, dischargeEvents, cancellationToken);
            }

            var processedEvents = await cernerListService.ProcessAdmits(facilityId, result.Message.Value, cancellationToken);

            if (processedEvents != null)
            {
                await _eventProducerService.ProduceEventsAsync(facilityId, processedEvents, cancellationToken);
            }
        }

        private static string FacilityIdOf(Message<string, CernerPatientsAcquired>? message) =>
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
                    var value = JsonSerializer.Deserialize<CernerPatientsAcquired>(valueBytes, new JsonSerializerOptions
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
}
