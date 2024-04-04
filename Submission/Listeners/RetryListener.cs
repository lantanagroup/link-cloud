using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Application.Utilities;
using Quartz;
using System.Text;

namespace LantanaGroup.Link.Report.Listeners
{
    public class RetryListener : BackgroundService
    {
        private readonly ILogger<RetryListener> _logger;

        private readonly IKafkaConsumerFactory<string, string>
            _kafkaConsumerFactory;

        private readonly ISchedulerFactory _schedulerFactory;
        private readonly RetryRepository _retryRepository;
        public RetryListener(ILogger<RetryListener> logger,
            IKafkaConsumerFactory<string, string> kafkaConsumerFactory,
            ISchedulerFactory schedulerFactory,
            RetryRepository retryRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _schedulerFactory = schedulerFactory ?? throw new ArgumentException(nameof(schedulerFactory));
            _retryRepository = retryRepository ?? throw new ArgumentException(nameof(retryRepository));
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }


        private async void StartConsumerLoop(CancellationToken cancellationToken)
        {
            var config = new ConsumerConfig()
            {
                GroupId = "SubmissionRetryService",
                EnableAutoCommit = false
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(config);
            try
            {
                consumer.Subscribe(KafkaTopic.SubmitReportRetry.GetStringValue());

                _logger.LogInformation($"Started Submission Service Retry consumer for topics: [{string.Join(", ", consumer.Subscription)}] {DateTime.UtcNow}");

                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        ConsumeResult<string, string> consumeResult = consumer.Consume(cancellationToken);

                        int retryCount = 1;

                        if (consumeResult.Message.Headers.TryGetLastBytes("X-Retry-Count", out var headerValue))
                        {
                            retryCount = int.Parse(Encoding.UTF8.GetString(headerValue)) + 1;
                        }

                        string correlationId = string.Empty;

                        if (consumeResult.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var correlationIdBytes))
                        {
                            correlationId = Encoding.UTF8.GetString(correlationIdBytes);
                        }

                        var retryEntity = new RetryEntity
                        {
                            Id = Guid.NewGuid().ToString(),
                            ClientId = config.GroupId,
                            ServiceName = "Submission",
                            FacilityId = consumeResult.Message.Key,
                            ScheduledTrigger = "",
                            Topic = consumeResult.Topic,
                            Key = consumeResult.Message.Key,
                            Value = consumeResult.Message.Value,
                            RetryCount = retryCount,
                            CorrelationId = correlationId,
                            Headers = consumeResult.Message.Headers,
                            CreateDate = DateTime.UtcNow
                        };

                        await _retryRepository.AddAsync(retryEntity, cancellationToken);

                        await RetryScheduleService.CreateJobAndTrigger(retryEntity,
                            await _schedulerFactory.GetScheduler(cancellationToken));

                        consumer.Commit(consumeResult);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation($"Stopped Report Service Retry consumer for topics: [{string.Join(", ", consumer.Subscription)}] at {DateTime.UtcNow}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in Report Service Retry consumer for topics: [{string.Join(", ", consumer.Subscription)}] at {DateTime.UtcNow}", ex);
                }
                finally
                {
                    consumer.Close();
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogError($"Operation Canceled: {oce.Message}", oce);
                consumer.Close();
                consumer.Dispose();
            }
        }
    }
}
