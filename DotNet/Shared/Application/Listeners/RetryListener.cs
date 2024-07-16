using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services;

using Quartz;
using System.Text;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Settings;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace LantanaGroup.Link.Shared.Application.Listeners
{
    public class RetryListener : BackgroundService
    {
        private readonly ILogger<RetryListener> _logger;

        private readonly IKafkaConsumerFactory<string, string>
            _kafkaConsumerFactory;

        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IEntityRepository<RetryEntity> _retryRepository;
        private readonly IOptions<ConsumerSettings> _consumerSettings;
        private readonly IRetryEntityFactory _retryEntityFactory;
        private readonly IDeadLetterExceptionHandler<string, string> _deadLetterExceptionHandler;
        private readonly RetryListenerSettings _retryListenerSettings;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public RetryListener(ILogger<RetryListener> logger,
            IKafkaConsumerFactory<string, string> kafkaConsumerFactory,
            ISchedulerFactory schedulerFactory,
            IOptions<ConsumerSettings> consumerSettings,
            IRetryEntityFactory retryEntityFactory,
            IDeadLetterExceptionHandler<string, string> deadLetterExceptionHandler,
            RetryListenerSettings retryListenerSettings,
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _schedulerFactory = schedulerFactory ?? throw new ArgumentException(nameof(schedulerFactory));
            _consumerSettings = consumerSettings ?? throw new ArgumentException(nameof(consumerSettings));
            _retryEntityFactory = retryEntityFactory ?? throw new ArgumentException(nameof(retryEntityFactory));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentException(nameof(serviceScopeFactory));
            _deadLetterExceptionHandler.ServiceName = retryListenerSettings.ServiceName;
            _retryListenerSettings = retryListenerSettings;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async void StartConsumerLoop(CancellationToken cancellationToken)
        {
            var config = new ConsumerConfig()
            {
                GroupId = _retryListenerSettings.ServiceName,
                EnableAutoCommit = false
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(config);

            try
            {
                consumer.Subscribe(_retryListenerSettings.Topics);

                _logger.LogInformation($"Started {_retryListenerSettings.ServiceName} retry consumer for topics: [{string.Join(", ", consumer.Subscription)}] {DateTime.UtcNow}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    ConsumeResult<string, string>? consumeResult;
                    
                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, cancellationToken) =>
                        {
                            consumeResult = result;

                            try
                            {
                                if (consumeResult.Message.Headers.TryGetLastBytes(KafkaConstants.HeaderConstants.ExceptionService, out var exceptionService))
                                {
                                    //If retry event is not from the exception service, disregard the retry event
                                    if (Encoding.UTF8.GetString(exceptionService) != _retryListenerSettings.ServiceName)
                                    {
                                        consumer.Commit(consumeResult);
                                        //continue;
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

                                using var scope = _serviceScopeFactory.CreateScope();

                                var _retryRepository = scope.ServiceProvider.GetRequiredService<IEntityRepository<RetryEntity>>();

                                var retryEntity = _retryEntityFactory.CreateRetryEntity(consumeResult, _consumerSettings.Value);

                                await _retryRepository.AddAsync(retryEntity, cancellationToken);

                                var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

                                await RetryScheduleService.CreateJobAndTrigger(retryEntity, scheduler);

                                consumer.Commit(consumeResult);
                            }
                            catch (DeadLetterException ex)
                            {
                                var facilityId = GetStringValueFromHeader(consumeResult.Message.Headers, KafkaConstants.HeaderConstants.ExceptionFacilityId);
                                _deadLetterExceptionHandler.Topic = consumeResult.Topic.Replace("-Retry", "-Error");
                                _deadLetterExceptionHandler.HandleException(consumeResult, ex, AuditEventType.Create, facilityId);
                                consumer.Commit(consumeResult);
                                //continue;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Error in {_retryListenerSettings.ServiceName} retry consumer for topics: [{string.Join(", ", consumer.Subscription)}] at {DateTime.UtcNow}");
                            }

                        }, cancellationToken);
                    }
                    catch (ConsumeException ex)
                    {
                        var facilityId = GetStringValueFromHeader(ex.ConsumerRecord.Message.Headers, KafkaConstants.HeaderConstants.ExceptionFacilityId);

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
                        _logger.LogError(ex, $"Error consuming message for topics: [{string.Join(", ", consumer.Subscription)}] at {DateTime.UtcNow}");
                        continue;
                    }                    
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogError(oce, $"Operation Cancelled: {oce.Message}");
                consumer.Close();
                consumer.Dispose();
            }
            
        }

        private static string GetStringValueFromHeader(Headers headers, string key)
        {
            string returnVal = string.Empty;

            if (headers.TryGetLastBytes(key, out var facilityIdBytes))
            {
                returnVal = Encoding.UTF8.GetString(facilityIdBytes);
            }

            return returnVal;
        }
    }
}
