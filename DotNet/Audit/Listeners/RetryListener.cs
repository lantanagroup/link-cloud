using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Audit.Application.Retry.Commands;
using LantanaGroup.Link.Audit.Infrastructure.Logging;
using LantanaGroup.Link.Audit.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using Quartz;
using System.Diagnostics;
using System.Text;

namespace LantanaGroup.Link.Audit.Listeners
{
    public class RetryListener : BackgroundService
    {
        private readonly ILogger<RetryListener> _logger;
        private readonly IKafkaConsumerFactory<string, string> _kafkaConsumerFactory;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptions<ConsumerSettings> _consumerSettings;
        private readonly IRetryEntityFactory _retryEntityFactory;
        private readonly IDeadLetterExceptionHandler<string, string> _deadLetterExceptionHandler;        

        public RetryListener(ILogger<RetryListener> logger, IKafkaConsumerFactory<string, string> kafkaConsumerFactory, ISchedulerFactory schedulerFactory, IOptions<ConsumerSettings> consumerSettings, IRetryEntityFactory retryEntityFactory, IDeadLetterExceptionHandler<string, string> deadLetterExceptionHandler, IServiceScopeFactory scopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _schedulerFactory = schedulerFactory ?? throw new ArgumentException(nameof(schedulerFactory));
            _scopeFactory = scopeFactory ?? throw new ArgumentException(nameof(scopeFactory));
            _consumerSettings = consumerSettings ?? throw new ArgumentException(nameof(consumerSettings));
            _retryEntityFactory = retryEntityFactory ?? throw new ArgumentException(nameof(retryEntityFactory));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));      
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        public async void StartConsumerLoop(CancellationToken cancellationToken)
        {
            var config = new ConsumerConfig()
            {
                GroupId = AuditConstants.ServiceName,
                EnableAutoCommit = false
            };

            using var _consumer = _kafkaConsumerFactory.CreateConsumer(config);

            try
            {
                _consumer.Subscribe(KafkaTopic.AuditableEventOccurredRetry.GetStringValue());
                _logger.LogConsumerStarted(nameof(KafkaTopic.AuditableEventOccurredRetry), DateTime.UtcNow);

                while (!cancellationToken.IsCancellationRequested)
                {
                    await _consumer.ConsumeWithInstrumentation(async (result, cancellationToken) =>
                    {
                        try
                        {
                            if (result is null || string.IsNullOrEmpty(result.Message.Key))
                            {
                                throw new DeadLetterException("Invalid Auditable Event", AuditEventType.Create);
                            }

                            if (result.Message.Headers.TryGetLastBytes(KafkaConstants.HeaderConstants.ExceptionService, out var exceptionService))
                            {
                                //If retry event is not from the Audit service, disregard the retry event
                                if (Encoding.UTF8.GetString(exceptionService) != AuditConstants.ServiceName)
                                {
                                    _consumer.Commit(result);                                    
                                }
                            }

                            if (result.Message.Headers.TryGetLastBytes(KafkaConstants.HeaderConstants.RetryCount, out var retryCount))
                            {
                                int countValue = int.Parse(Encoding.UTF8.GetString(retryCount));

                                //Dead letter if the retry count exceeds the configured retry duration count
                                if (countValue >= _consumerSettings.Value.ConsumerRetryDuration.Count)
                                {
                                    throw new DeadLetterException($"Retry count exceeded for message with key: {result.Message.Key}", AuditEventType.Create);
                                }
                            }

                            var retryEntity = _retryEntityFactory.CreateRetryEntity(result, _consumerSettings.Value);

                            using (var scope = _scopeFactory.CreateScope())
                            {
                                var _createRetryCommand = scope.ServiceProvider.GetRequiredService<ICreateRetryEntity>();
                                bool outcome = await _createRetryCommand.Execute(retryEntity, cancellationToken);

                                if(!outcome)
                                {
                                    throw new Exception("Failed to create retry entity");
                                }
                            }                            

                            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

                            await RetryScheduleService.CreateJobAndTrigger(retryEntity, scheduler);

                            _consumer.Commit(result);
                        }
                        catch (DeadLetterException ex)
                        {
                            var facilityId = result is not null ? GetFacilityIdFromHeader(result.Message.Headers) : string.Empty;
                            _deadLetterExceptionHandler.Topic = $"{nameof(KafkaTopic.AuditableEventOccurred)}-Error";
                            _deadLetterExceptionHandler.HandleException(result, ex, AuditEventType.Create, facilityId);
                            _consumer.Commit(result);                            
                        }
                        catch (Exception ex)
                        {
                            _logger.LogConsumerException(nameof(KafkaTopic.AuditableEventOccurredRetry), ex.Message);                            
                        }

                    }, cancellationToken);
                }
            }
            catch (OperationCanceledException oce)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(oce);
                _logger.LogOperationCanceledException(nameof(KafkaTopic.AuditableEventOccurredRetry), oce.Message);
                _consumer.Close();
                _consumer.Dispose();
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
