using Confluent.Kafka;
using Hl7.FhirPath.Sprache;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Quartz;
using System.Text;

namespace LantanaGroup.Link.Submission.Listeners
{
    public class RetryListener : BackgroundService
    {
        private readonly ILogger<RetryListener> _logger;

        private readonly IKafkaConsumerFactory<string, string>
            _kafkaConsumerFactory;

        private readonly ISchedulerFactory _schedulerFactory;
        private readonly RetryRepository _retryRepository;
        private readonly IOptions<ConsumerSettings> _consumerSettings;
        public RetryListener(ILogger<RetryListener> logger,
            IKafkaConsumerFactory<string, string> kafkaConsumerFactory,
            ISchedulerFactory schedulerFactory,
            RetryRepository retryRepository,
            IOptions<ConsumerSettings> consumerSettings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _schedulerFactory = schedulerFactory ?? throw new ArgumentException(nameof(schedulerFactory));
            _retryRepository = retryRepository ?? throw new ArgumentException(nameof(retryRepository));
            _consumerSettings = consumerSettings ?? throw new ArgumentException(nameof(consumerSettings));
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
                            int count = int.Parse(Encoding.UTF8.GetString(headerValue)); 

                            if (count == _consumerSettings.Value.ConsumerRetryDuration.Count())
                            {
                                _logger.LogError($"Retry count exceeded for message with key: {consumeResult.Message.Key}");
                                consumer.Commit(consumeResult);
                                continue;
                            }

                            retryCount = count+=1;
                        }

                        Dictionary<string, string>headers = new Dictionary<string, string>();
                        foreach (var header in consumeResult.Message.Headers)
                        {
                            headers.Add(header.Key, Encoding.UTF8.GetString(header.GetValueBytes()));
                        }

                        var retryEntity = new RetryEntity
                        {
                            ClientId = config.GroupId,
                            ServiceName = "Submission",
                            FacilityId = consumeResult.Message.Key,
                            ScheduledTrigger = _consumerSettings.Value.ConsumerRetryDuration[retryCount - 1],
                            Topic = consumeResult.Topic,
                            Key = consumeResult.Message.Key,
                            Value = consumeResult.Message.Value,
                            RetryCount = retryCount,
                            CorrelationId = headers.FirstOrDefault(x => x.Key == "X-Correlation-Id").Value ?? "",
                            Headers = headers,
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
