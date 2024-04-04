using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using Quartz;
using System.Text;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.Report.Listeners
{
    public class RetryListener : BackgroundService
    {
        private readonly ILogger<RetryListener> _logger;

        private readonly IKafkaConsumerFactory<string, string>
            _kafkaConsumerFactory;

        private readonly ISchedulerFactory _schedulerFactory;

        public RetryListener(ILogger<RetryListener> logger,
            IKafkaConsumerFactory<string, string> kafkaConsumerFactory,
            ISchedulerFactory schedulerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _schedulerFactory = schedulerFactory ?? throw new ArgumentException(nameof(schedulerFactory));
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }


        private async void StartConsumerLoop(CancellationToken cancellationToken)
        {
            var config = new ConsumerConfig()
            {
                GroupId = "ReportScheduledEvent",
                EnableAutoCommit = false
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(config);
            try
            {
                consumer.Subscribe(KafkaTopic.SubmitReportRetry.GetStringValue());
                
                _logger.LogInformation($"Started Report Service Retry consumer for topics: [{string.Join(", ", consumer.Subscription)}] {DateTime.UtcNow}");

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
                            Id = Guid.NewGuid(),
                            ClientId = config.GroupId,
                            ServiceName = "Submission",
                            FacilityId = consumeResult.Message.Key,
                            Topic = consumeResult.Topic,
                            Offset = consumeResult.Offset.Value,
                            Partition = consumeResult.Partition.Value,
                            Key = consumeResult.Message.Key,
                            Value = consumeResult.Message.Value,
                            RetryCount = retryCount,
                            CorrelationId = correlationId,
                            Headers = consumeResult.Message.Headers,
                            CreateDate = DateTime.UtcNow
                        };

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
