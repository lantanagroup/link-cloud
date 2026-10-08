using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using System.Text;

namespace LantanaGroup.Link.Shared.Application.Listeners
{
    public class RetryListener : BackgroundService
    {
        private readonly ILogger<RetryListener> _logger;

        private readonly IKafkaConsumerFactory<string, string> _kafkaConsumerFactory;

        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IOptions<ConsumerSettings> _consumerSettings;
        private readonly IRetryModelFactory _retryEntityFactory;
        private readonly IDeadLetterExceptionHandler<RetryListener, string, string> _deadLetterExceptionHandler;
        private readonly RetryListenerSettings _retryListenerSettings;
        private readonly ServiceInformation _serviceInformation;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public RetryListener(ILogger<RetryListener> logger,
            IKafkaConsumerFactory<string, string> kafkaConsumerFactory,
            ISchedulerFactory schedulerFactory,
            IOptions<ConsumerSettings> consumerSettings,
            IRetryModelFactory retryEntityFactory,
            IDeadLetterExceptionHandler<RetryListener, string, string> deadLetterExceptionHandler,
            RetryListenerSettings retryListenerSettings,
            ServiceInformation serviceInformation,
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _schedulerFactory = schedulerFactory ?? throw new ArgumentException(nameof(schedulerFactory));
            _consumerSettings = consumerSettings ?? throw new ArgumentException(nameof(consumerSettings));
            _retryEntityFactory = retryEntityFactory ?? throw new ArgumentException(nameof(retryEntityFactory));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentException(nameof(serviceScopeFactory));
            _retryListenerSettings = retryListenerSettings ?? throw new ArgumentException(nameof(retryListenerSettings));
            _serviceInformation = serviceInformation ?? throw new ArgumentException(nameof(serviceInformation));
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async Task StartConsumerLoop(CancellationToken cancellationToken)
        {
            var serviceName = string.IsNullOrWhiteSpace(_serviceInformation.ServiceConfigName)
                ? _serviceInformation.ServiceName
                : _serviceInformation.ServiceConfigName;
            var config = new ConsumerConfig()
            {
                GroupId = serviceName + "-retry",
                EnableAutoCommit = false
            };

            var assignmentTracker = new KafkaAssignmentTracker();
            using var consumer = _kafkaConsumerFactory.CreateConsumer(config, assignmentTracker: assignmentTracker);

            try
            {
                var topics = _retryListenerSettings.Topics
                    .Select(topic => ToServiceRetryTopic(topic, serviceName))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                consumer.Subscribe(topics);

                _logger.LogInformation("Started {ServiceName} retry consumer for topics: [{Topics}] {Timestamp}", _serviceInformation.ServiceConfigName, string.Join(", ", consumer.Subscription), DateTime.UtcNow);

                while (!cancellationToken.IsCancellationRequested)
                {
                    ConsumeResult<string, string>? consumeResult;

                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                        {
                            consumeResult = result;
                            var accounted = false;

                            try
                            {
                                if (consumeResult.Message.Headers.TryGetLastBytes(KafkaConstants.HeaderConstants.ExceptionService, out var exceptionService))
                                {
                                    //If retry event is not from the exception service, disregard the retry event
                                    if (Encoding.UTF8.GetString(exceptionService) != _serviceInformation.ServiceConfigName)
                                    {
                                        _logger.LogWarning("Service that Retry instance is running in ({instanceServiceName}) is different from the service that produced the message ({messageServiceName}). Message will be disregarded.", _serviceInformation.ServiceConfigName, Encoding.UTF8.GetString(exceptionService));
                                        accounted = true;
                                        return;
                                    }
                                }

                                if (consumeResult.Message.Headers.TryGetLastBytes(KafkaConstants.HeaderConstants.RetryCount, out var retryCount))
                                {
                                    int countValue = int.Parse(Encoding.UTF8.GetString(retryCount));

                                    //Dead letter if the retry count exceeds the configured retry duration count
                                    if (countValue > _consumerSettings.Value.ConsumerRetryDuration.Count())
                                    {
                                        throw new DeadLetterException($"Retry count exceeded for message with key: {consumeResult.Message.Key}");
                                    }
                                }

                                using var scope = _serviceScopeFactory.CreateScope();

                                var retryModel = _retryEntityFactory.CreateRetryModel(consumeResult, _consumerSettings.Value);

                                var scheduler = await _schedulerFactory.GetScheduler(consumeCancellationToken);

                                _logger.LogInformation("Scheduling retry for {Topic}-{Id} at {ScheduledTrigger}, Retry Count: {RetryCount}", retryModel.Topic, retryModel.Id, retryModel.ScheduledTrigger, retryModel.RetryCount);

                                await RetryScheduleService.CreateJobAndTrigger(retryModel, scheduler, consumeCancellationToken);
                                accounted = true;
                            }
                            catch (DeadLetterException ex)
                            {
                                var facilityId = GetStringValueFromHeader(consumeResult.Message.Headers, KafkaConstants.HeaderConstants.ExceptionFacilityId);
                                var mainTopic = KafkaTopicNames.TryMainFromRetry(consumeResult.Topic, out var parsed, out _)
                                    ? parsed
                                    : consumeResult.Topic;
                                _deadLetterExceptionHandler.Topic = KafkaTopicNames.Error(mainTopic);
                                _deadLetterExceptionHandler.HandleException(consumeResult, ex, facilityId);
                                accounted = true;
                            }
                            catch (OperationCanceledException) when (consumeCancellationToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error in {ServiceName} retry consumer for topics: [{Topics}] at {Timestamp}", _serviceInformation.ServiceConfigName, string.Join(", ", consumer.Subscription), DateTime.UtcNow);
                                // A later commit on this partition would cover this offset. The main topic
                                // is already committed, so rewind and try the schedule again.
                                if (consumeResult != null)
                                {
                                    try
                                    {
                                        consumer.Seek(consumeResult.TopicPartitionOffset);
                                    }
                                    catch (KafkaException seekEx)
                                    {
                                        _logger.LogError(seekEx, "Failed to rewind retry message {TopicPartitionOffset}.", consumeResult.TopicPartitionOffset);
                                    }
                                }

                                await Task.Delay(TimeSpan.FromSeconds(1), consumeCancellationToken);
                            }
                            finally
                            {
                                if (accounted && consumeResult != null && !consumeCancellationToken.IsCancellationRequested)
                                {
                                    assignmentTracker.MarkProcessed(consumeResult);
                                    consumer.SafeCommit(consumeResult, _logger);
                                }
                            }

                        }, cancellationToken);
                    }
                    catch (ConsumeException ex)
                    {
                        var facilityId = GetStringValueFromHeader(ex.ConsumerRecord.Message.Headers, KafkaConstants.HeaderConstants.ExceptionFacilityId);

                        var failedTopic = ex.ConsumerRecord.Topic ?? string.Empty;
                        var mainTopic = KafkaTopicNames.TryMainFromRetry(failedTopic, out var parsed, out _)
                            ? parsed
                            : failedTopic;
                        _deadLetterExceptionHandler.Topic = KafkaTopicNames.Error(mainTopic);
                        _deadLetterExceptionHandler.HandleConsumeException(ex, facilityId);
                        _logger.LogError(ex, "Error consuming message for topics: [{Topics}] at {Timestamp}", string.Join(", ", consumer.Subscription), DateTime.UtcNow);
                        continue;
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogError(oce, "Operation Cancelled: {Message}", oce.Message);
                consumer.Close();
                consumer.Dispose();
            }

        }

        private static string ToServiceRetryTopic(string topic, string serviceName)
        {
            if (KafkaTopicNames.TryMainFromRetry(topic, out _, out _))
            {
                return topic;
            }

            const string sharedSuffix = "-Retry";
            var main = topic.EndsWith(sharedSuffix, StringComparison.Ordinal)
                ? topic[..^sharedSuffix.Length]
                : topic;
            return KafkaTopicNames.Retry(main, serviceName);
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
