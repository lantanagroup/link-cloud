using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Settings;
using QueryDispatch.Application.Settings;
using QueryDispatch.Domain.Managers;
using System.Text;

namespace LantanaGroup.Link.QueryDispatch.Listeners
{
    public class ReportScheduledEventListener : BackgroundService
    {
        private readonly ILogger<ReportScheduledEventListener> _logger;
        private readonly IKafkaConsumerFactory<string, ReportScheduledValue> _kafkaConsumerFactory;
        private readonly IQueryDispatchFactory _queryDispatchFactory;
        private readonly IProducer<string, AuditEventMessage> _auditProducer;
        private readonly IDeadLetterExceptionHandler<string, ReportScheduledValue> _deadLetterExceptionHandler;
        private readonly IDeadLetterExceptionHandler<string, string> _consumeResultDeadLetterExceptionHandler;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public ReportScheduledEventListener(
            ILogger<ReportScheduledEventListener> logger,
            IKafkaConsumerFactory<string, ReportScheduledValue> kafkaConsumerFactory,
            IQueryDispatchFactory queryDispatchFactory, 
            IProducer<string, AuditEventMessage> auditProducer, 
            IDeadLetterExceptionHandler<string, ReportScheduledValue> deadLetterExceptionHandler,
            IDeadLetterExceptionHandler<string, string> consumeResultDeadLetterExceptionHandler,
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _queryDispatchFactory = queryDispatchFactory;
            _auditProducer = auditProducer;
            _deadLetterExceptionHandler = deadLetterExceptionHandler;
            _consumeResultDeadLetterExceptionHandler = consumeResultDeadLetterExceptionHandler;
            _serviceScopeFactory = serviceScopeFactory;

            _deadLetterExceptionHandler.ServiceName = "QueryDispatch";
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.ReportScheduled) + "-Error";

            _consumeResultDeadLetterExceptionHandler.ServiceName = "QueryDispatch";
            _consumeResultDeadLetterExceptionHandler.Topic = nameof(KafkaTopic.ReportScheduled) + "-Error";
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async void StartConsumerLoop(CancellationToken cancellationToken)
        {

            var config = new ConsumerConfig()
            {
                GroupId = QueryDispatchConstants.ServiceName,
                EnableAutoCommit = false
            };

            using (var _reportScheduledConsumer = _kafkaConsumerFactory.CreateConsumer(config))
            {
                try
                {
                    _reportScheduledConsumer.Subscribe(nameof(KafkaTopic.ReportScheduled));
                    _logger.LogInformation("Started query dispatch consumer for topic '{reportScheduled}' at {date}", KafkaTopic.ReportScheduled, DateTime.UtcNow);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        ConsumeResult<string, ReportScheduledValue>? consumeResult;

                        try
                        {
                            await _reportScheduledConsumer.ConsumeWithInstrumentation(async (result, cancellationToken) =>
                            {
                                consumeResult = result;

                                try
                                {
                                    using var scope = _serviceScopeFactory.CreateScope();

                                    var scheduledReportMgr = scope.ServiceProvider.GetRequiredService<IScheduledReportManager>();

                                    var scheduledReportRepo = scope.ServiceProvider.GetRequiredService<IEntityRepository<ScheduledReportEntity>>();

                                    ReportScheduledValue value = consumeResult.Message.Value;

                                    if (consumeResult == null
                                    || string.IsNullOrWhiteSpace(consumeResult.Message.Key)
                                    || !value.IsValid())
                                    {
                                        throw new DeadLetterException("Invalid Report Scheduled event");
                                    }

                                    string reportTrackingId = value.ReportTrackingId;

                                    string key = consumeResult.Message.Key;

                                    var startDate = value.StartDate.UtcDateTime;
                                    var endDate = value.EndDate.UtcDateTime;
                                    var frequency = value.Frequency.ToString();

                                    _logger.LogInformation("Consumed Event for: Facility '{FacilityId}' has a report type of '{ReportType}' with a report period of {startDate} to {endDate}", key, value.ReportTypes, startDate, endDate);

                                    var existingRecord = await scheduledReportRepo.FirstOrDefaultAsync(x => x.FacilityId == key);

                                    if (existingRecord != null)
                                    {
                                        _logger.LogInformation("Facility {facilityId} found", key);
										
                                        ScheduledReportEntity scheduledReport = _queryDispatchFactory.CreateScheduledReport(key, value.ReportTypes, frequency, startDate, endDate, reportTrackingId);
                                        await scheduledReportMgr.UpdateScheduledReport(existingRecord, scheduledReport);
                                    }
                                    else
                                    {
                                        ScheduledReportEntity scheduledReport = _queryDispatchFactory.CreateScheduledReport(key, value.ReportTypes, frequency, startDate, endDate, reportTrackingId);
                                        await scheduledReportMgr.createScheduledReport(scheduledReport);                                     
                                    }

                                    _reportScheduledConsumer.Commit(consumeResult);

                                }
                                catch (DeadLetterException ex)
                                {
                                    _deadLetterExceptionHandler.HandleException(consumeResult, ex, consumeResult.Key);
                                    _reportScheduledConsumer.Commit(consumeResult);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, $"Failed to process Report Scheduled event.");

                                    var auditValue = new AuditEventMessage
                                    {
                                        FacilityId = consumeResult.Message.Key,
                                        Action = AuditEventType.Query,
                                        ServiceName = "QueryDispatch",
                                        EventDate = DateTime.UtcNow,
                                        Notes = $"Report Scheduled event processing failure \nException Message: {ex}",
                                    };

                                    ProduceAuditEvent(auditValue, consumeResult.Message.Headers);

                                    _deadLetterExceptionHandler.HandleException(consumeResult, new DeadLetterException("Query Dispatch Exception thrown: " + ex.Message), consumeResult.Message.Key);

                                    _reportScheduledConsumer.Commit(consumeResult);
                                }

                            }, cancellationToken);
                        }
                        catch (ConsumeException ex)
                        {
                            _logger.LogError(ex, "Error consuming message for topics: [{1}] at {2}", string.Join(", ", _reportScheduledConsumer.Subscription), DateTime.UtcNow);

                            if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                            {
                                throw new OperationCanceledException(ex.Error.Reason, ex);
                            }

                            var facilityId = GetFacilityIdFromHeader(ex.ConsumerRecord.Message.Headers);

                            _deadLetterExceptionHandler.HandleConsumeException(ex, facilityId);

                            var offset = ex.ConsumerRecord?.TopicPartitionOffset;
                            _reportScheduledConsumer.Commit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset });
                        }
                    }

                    _reportScheduledConsumer.Close();
                    _reportScheduledConsumer.Dispose();
                }
                catch (OperationCanceledException oce)
                {
                    _logger.LogError(oce, $"Operation Canceled: {oce.Message}");
                    _reportScheduledConsumer.Close();
                    _reportScheduledConsumer.Dispose();
                }
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

        private void ProduceAuditEvent(AuditEventMessage auditEvent, Headers headers)
        {
            _auditProducer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
            {
                Value = auditEvent,
                Headers = headers
            });

        }
    }
}