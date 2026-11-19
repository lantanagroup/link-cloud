using Confluent.Kafka;
using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Infrastructure.Logging;
using LantanaGroup.Link.Audit.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using System.Diagnostics;
using System.Text;

namespace LantanaGroup.Link.Audit.Listeners
{
    public class AuditEventListener : BackgroundService
    {
        private readonly ILogger<AuditEventListener> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IKafkaConsumerFactory<string, AuditEventMessage> _kafkaConsumerFactory;
        private readonly IDeadLetterExceptionHandler<AuditEventListener, string, AuditEventMessage> _deadLetterExceptionHandler;
        private readonly ITransientExceptionHandler<AuditEventListener, string, AuditEventMessage> _transientExceptionHandler;

        public AuditEventListener(ILogger<AuditEventListener> logger, IServiceScopeFactory scopeFactory, IKafkaConsumerFactory<string,
            AuditEventMessage> kafkaConsumerFactory, IDeadLetterExceptionHandler<AuditEventListener, string, AuditEventMessage> deadLetterExceptionHandler,
            ITransientExceptionHandler<AuditEventListener, string, AuditEventMessage> transientExceptionHandler)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentNullException(nameof(kafkaConsumerFactory));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentNullException(nameof(deadLetterExceptionHandler));
            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentNullException(nameof(transientExceptionHandler));

            //configure deadletter exception handlers
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.AuditableEventOccurred) + "-Error";

            //configure transient exception handler
            _transientExceptionHandler.Topic = nameof(KafkaTopic.AuditableEventOccurred) + "-Retry";
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async Task StartConsumerLoop(CancellationToken cancellationToken)
        {
            var config = new ConsumerConfig()
            {
                GroupId = AuditConstants.ServiceName,
                EnableAutoCommit = false
            };

            var assignmentTracker = new KafkaAssignmentTracker();
            using (var _consumer = _kafkaConsumerFactory.CreateConsumer(config, assignmentTracker: assignmentTracker))
            {
                try
                {
                    _consumer.Subscribe(KafkaTopicNames.Subscription(nameof(KafkaTopic.AuditableEventOccurred), AuditConstants.ServiceName));
                    _logger.LogConsumerStarted(nameof(KafkaTopic.AuditableEventOccurred), DateTime.UtcNow);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        ConsumeResult<string, AuditEventMessage>? result = null;
                        try
                        {
                            result = _consumer.Consume(cancellationToken);
                            var accounted = false;

                            try
                            {
                                var _auditEventProcessor = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<IAuditEventProcessor>();
                                _ = await _auditEventProcessor.ProcessAuditEvent(result, cancellationToken);
                                accounted = true;
                            }
                            catch (DeadLetterException ex)
                            {
                                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                                Activity.Current?.AddException(ex);

                                var facilityId = KafkaIdentity.Facility(result?.Message?.Value?.FacilityId, result?.Message?.Key);
                                accounted = result != null && await DeadLetterCommit.AccountAsync(
                                    _deadLetterExceptionHandler.HandleException(result, ex, facilityId ?? string.Empty),
                                    _consumer,
                                    result,
                                    _logger,
                                    cancellationToken);
                            }
                            catch (TransientException ex)
                            {
                                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                                Activity.Current?.AddException(ex);
                                var facilityId = KafkaIdentity.Facility(result?.Message?.Value?.FacilityId, result?.Message?.Key);
                                _transientExceptionHandler.HandleException(result, ex, facilityId ?? string.Empty);
                                accounted = true;
                            }
                            finally
                            {
                                if (accounted && result != null && !cancellationToken.IsCancellationRequested)
                                {
                                    assignmentTracker.MarkProcessed(result);
                                    _consumer.SafeCommit(result, _logger);
                                }
                            }
                        }
                        catch (ConsumeException ex)
                        {
                            Activity.Current?.SetStatus(ActivityStatusCode.Error);
                            Activity.Current?.AddException(ex);
                            _logger.LogConsumerException(nameof(KafkaTopic.AuditableEventOccurred), ex.Message);

                            if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                            {
                                throw new OperationCanceledException(ex.Error.Reason, ex);
                            }

                            var rawKey = ex.ConsumerRecord?.Message?.Key != null ? Encoding.UTF8.GetString(ex.ConsumerRecord.Message.Key) : null;
                            var facilityId = KafkaIdentity.Facility(null, rawKey) ?? string.Empty;

                            _deadLetterExceptionHandler.HandleConsumeException(ex, facilityId);

                            var offset = ex.ConsumerRecord?.TopicPartitionOffset;
                            _consumer.SafeCommit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset }, _logger);
                        }
                    }

                    _consumer.Close();
                    _consumer.Dispose();

                }
                catch (OperationCanceledException oce)
                {
                    Activity.Current?.SetStatus(ActivityStatusCode.Error);
                    Activity.Current?.AddException(oce);
                    _logger.LogOperationCanceledException(nameof(KafkaTopic.AuditableEventOccurred), oce.Message);
                    _consumer.Close();
                    _consumer.Dispose();
                }
            }
        }

    }
}
