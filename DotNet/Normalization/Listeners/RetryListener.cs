using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.Extensions.Options;
using Quartz;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Normalization.Listeners
{
    public class RetryListener : BackgroundService
    {
        private readonly ILogger<RetryListener> _logger;
        private readonly IOptions<ServiceInformation> _serviceInformation;
        private readonly IKafkaConsumerFactory<string, string> _kafkaConsumerFactory;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IRetryRepository _retryRepository;
        private readonly IOptions<ConsumerSettings> _consumerSettings;
        private readonly IRetryEntityFactory _retryEntityFactory;
        private readonly IDeadLetterExceptionHandler<string, string> _deadLetterExceptionHandler;

        public RetryListener(ILogger<RetryListener> logger,
            IOptions<ServiceInformation> serviceInformation,
            IKafkaConsumerFactory<string, string> kafkaConsumerFactory,
            ISchedulerFactory schedulerFactory,
            IRetryRepository retryRepository,
            IOptions<ConsumerSettings> consumerSettings,
            IRetryEntityFactory retryEntityFactory,
            IDeadLetterExceptionHandler<string, string> deadLetterExceptionHandler)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceInformation = serviceInformation ?? throw new ArgumentNullException(nameof(serviceInformation));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _schedulerFactory = schedulerFactory ?? throw new ArgumentException(nameof(schedulerFactory));
            _retryRepository = retryRepository ?? throw new ArgumentException(nameof(retryRepository));
            _consumerSettings = consumerSettings ?? throw new ArgumentException(nameof(consumerSettings));
            _retryEntityFactory = retryEntityFactory ?? throw new ArgumentException(nameof(retryEntityFactory));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _deadLetterExceptionHandler.ServiceName = serviceInformation.Value.Name;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async void StartConsumerLoop(CancellationToken cancellationToken)
        {
            var config = new ConsumerConfig()
            {
                GroupId = "NormalizationRetry",
                EnableAutoCommit = false
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(config);
            try
            {
                consumer.Subscribe(KafkaTopic.ResourceAcquired + "-Retry"); // fixed this later

                _logger.LogInformation($"Started Normalization Service Retry consumer for topics: [{string.Join(", ", consumer.Subscription)}] {DateTime.UtcNow}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    ConsumeResult<string, string>? consumeResult;

                    try
                    {                        
                        consumeResult = await consumer.ConsumeWithInstrumentation((result, CancellationToken) =>
                        {
                            return Task.FromResult(result);
                        }, cancellationToken);
                    }
                    catch (ConsumeException ex)
                    {
                        var facilityId = GetFacilityIdFromHeader(ex.ConsumerRecord.Message.Headers);
                        var exceptionConsumerResult = new ConsumeResult<string, string>()
                        {
                            Message = new Message<string, string>()
                            {
                                Headers = ex.ConsumerRecord.Message.Headers,
                                Key = ex.ConsumerRecord != null && ex.ConsumerRecord.Message != null && ex.ConsumerRecord.Message.Key != null ? Encoding.UTF8.GetString(ex.ConsumerRecord.Message.Key) : string.Empty,
                                Value = ex.ConsumerRecord != null && ex.ConsumerRecord.Message != null && ex.ConsumerRecord.Message.Value != null ? Encoding.UTF8.GetString(ex.ConsumerRecord.Message.Value) : string.Empty,
                            },
                        };

                        _deadLetterExceptionHandler.Topic = ex.ConsumerRecord.Topic.Replace("-Retry", "-Error");
                        _deadLetterExceptionHandler.HandleException(exceptionConsumerResult, ex, AuditEventType.Create, facilityId);
                        _logger.LogError($"Error consuming message for topics: [{string.Join(", ", consumer.Subscription)}] at {DateTime.UtcNow}", ex);
                        continue;
                    }

                    try
                    {
                        if (consumeResult.Message.Headers.TryGetLastBytes(KafkaConstants.HeaderConstants.ExceptionService, out var exceptionService))
                        {
                            //If retry event is not from the Normalization service, disregard the retry event
                            if (Encoding.UTF8.GetString(exceptionService) != _serviceInformation.Value.Name)
                            {
                                consumer.Commit(consumeResult);
                                continue;
                            }
                        }

                        if (consumeResult.Message.Headers.TryGetLastBytes(KafkaConstants.HeaderConstants.RetryCount, out var retryCount))
                        {
                            int countValue = int.Parse(Encoding.UTF8.GetString(retryCount));

                            //Dead letter if the retry count exceeds the configured retry duration count
                            if (countValue >= _consumerSettings.Value.ConsumerRetryDuration.Count())
                            {
                                throw new DeadLetterException($"Retry count exceeded for message with key: {consumeResult.Message.Key}", AuditEventType.Create);
                            }
                        }

                        var retryEntity = _retryEntityFactory.CreateRetryEntity(consumeResult, _consumerSettings.Value);

                        await _retryRepository.AddAsync(retryEntity, cancellationToken);

                        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

                        await RetryScheduleService.CreateJobAndTrigger(retryEntity, scheduler);

                        consumer.Commit(consumeResult);
                    }
                    catch (DeadLetterException ex)
                    {
                        var facilityId = GetFacilityIdFromHeader(consumeResult.Message.Headers);
                        _deadLetterExceptionHandler.Topic = consumeResult.Topic.Replace("-Retry", "-Error");
                        _deadLetterExceptionHandler.HandleException(consumeResult, ex, AuditEventType.Create, facilityId);
                        consumer.Commit(consumeResult);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error in Normalization Service Retry consumer for topics: [{string.Join(", ", consumer.Subscription)}] at {DateTime.UtcNow}", ex);
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogError($"Operation Cancelled: {oce.Message}", oce);
                consumer.Close();
                consumer.Dispose();
            }
        }

        private static string GetFacilityIdFromHeader(Headers headers)
        {
            string facilityId = string.Empty;

            if (headers.TryGetLastBytes(KafkaConstants.HeaderConstants.ExceptionFacilityId, out var facilityIdBytes))
            {
                facilityId = Encoding.UTF8.GetString(facilityIdBytes);
            }

            return facilityId;
        }
    }
}
