
using Confluent.Kafka;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models;
using System.Threading;
using LantanaGroup.Link.Census.Application.Services;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
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

            using var consumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig);
            try
            {
                consumer.Subscribe(nameof(KafkaTopic.CernerPatientsAcquired));
                _logger.LogInformation("Started {name} consumer on {date} for topic '{TopicName}'", ClassName, DateTime.UtcNow, nameof(KafkaTopic.CernerPatientsAcquired));

                while (!cancellationToken.IsCancellationRequested)
                {
                    string facilityId = string.Empty;

                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                        {
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
                                    consumer.Commit(result);
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

                        _deadLetterExceptionHandler.HandleConsumeException(ex, facilityId);

                        var offset = ex.ConsumerRecord?.TopicPartitionOffset;
                        consumer.Commit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset });
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error encountered in CernerPatientsAcquiredListener");
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

        public async Task ProcessMessageAsync(ConsumeResult<string, CernerPatientsAcquired> result, CancellationToken cancellationToken)
        {
            if (result.Message.Value == null)
            {
                throw new DeadLetterException($"{ClassName}: CernerPatientsAcquired event value segment missing");
            }

            _logger.LogDebug("Consuming Event (Facility = {FacilityId})", result.Message.Key);

            using var scope = _scopeFactory.CreateScope();
            var cernerListService = scope.ServiceProvider.GetRequiredService<ICernerListService>();

            var dischargeEvents = await cernerListService.ProcessDischarges(result.Message.Key, result.Message.Value, cancellationToken);

            if (dischargeEvents != null)
            {
                await _eventProducerService.ProduceEventsAsync(result.Message.Key, dischargeEvents, cancellationToken);
            }

            var processedEvents = await cernerListService.ProcessAdmits(result.Message.Key, result.Message.Value, cancellationToken);

            if (processedEvents != null)
            {
                await _eventProducerService.ProduceEventsAsync(result.Message.Key, processedEvents, cancellationToken);
            }
        }
    }
}
