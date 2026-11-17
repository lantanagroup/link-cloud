using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
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
        private readonly IDeadLetterExceptionHandler<ReportScheduledEventListener, string, ReportScheduledValue> _deadLetterExceptionHandler;
        private readonly IDeadLetterExceptionHandler<ReportScheduledEventListener, string, string> _consumeResultDeadLetterExceptionHandler;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public ReportScheduledEventListener(
            ILogger<ReportScheduledEventListener> logger,
            IKafkaConsumerFactory<string, ReportScheduledValue> kafkaConsumerFactory,
            IQueryDispatchFactory queryDispatchFactory,
            IProducer<string, AuditEventMessage> auditProducer,
            IDeadLetterExceptionHandler<ReportScheduledEventListener, string, ReportScheduledValue> deadLetterExceptionHandler,
            IDeadLetterExceptionHandler<ReportScheduledEventListener, string, string> consumeResultDeadLetterExceptionHandler,
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _queryDispatchFactory = queryDispatchFactory;
            _auditProducer = auditProducer;
            _deadLetterExceptionHandler = deadLetterExceptionHandler;
            _consumeResultDeadLetterExceptionHandler = consumeResultDeadLetterExceptionHandler;
            _serviceScopeFactory = serviceScopeFactory;

            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.ReportScheduled) + "-Error";

            _consumeResultDeadLetterExceptionHandler.Topic = nameof(KafkaTopic.ReportScheduled) + "-Error";
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async Task StartConsumerLoop(CancellationToken cancellationToken)
        {

            var config = new ConsumerConfig()
            {
                GroupId = QueryDispatchConstants.ServiceName,
                EnableAutoCommit = false
            };

            var assignmentTracker = new KafkaAssignmentTracker();
            using (var _reportScheduledConsumer = _kafkaConsumerFactory.CreateConsumer(config, assignmentTracker: assignmentTracker))
            {
                try
                {
                    _reportScheduledConsumer.Subscribe(KafkaTopicNames.Subscription(nameof(KafkaTopic.ReportScheduled), QueryDispatchConstants.ServiceName));
                    _logger.LogInformation("Started query dispatch consumer for topic '{reportScheduled}' at {date}", KafkaTopic.ReportScheduled, DateTime.UtcNow);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        ConsumeResult<string, ReportScheduledValue>? consumeResult = null;

                        try
                        {
                            await _reportScheduledConsumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                            {
                                consumeResult = result;
                                var accounted = false;
                                string? facilityId = null;

                                try
                                {
                                    ReportScheduledValue? value = consumeResult?.Message?.Value;
                                    facilityId = KafkaIdentity.Facility(value?.FacilityId, consumeResult?.Message?.Key);

                                    if (consumeResult == null
                                    || value == null
                                    || string.IsNullOrWhiteSpace(facilityId)
                                    || !value.IsValid())
                                    {
                                        throw new DeadLetterException("Invalid Report Scheduled event");
                                    }

                                    using var scope = _serviceScopeFactory.CreateScope();

                                    var scheduledReportMgr = scope.ServiceProvider.GetRequiredService<IScheduledReportManager>();

                                    var scheduledReportRepo = scope.ServiceProvider.GetRequiredService<IBaseEntityRepository<ScheduledReportEntity>>();

                                    var reportTrackingId = value.ReportTrackingId?.ToString();

                                    var startDate = value.StartDate.UtcDateTime;
                                    var endDate = value.EndDate.UtcDateTime;
                                    var frequency = value.Frequency;

                                    _logger.LogInformation("Consumed Event for: Facility '{FacilityId}' has a report type of '{ReportType}' with a report period of {startDate} to {endDate}", facilityId, value.ReportTypes, startDate, endDate);

                                    var existingRecord = await scheduledReportRepo.FirstOrDefaultAsync(x => x.FacilityId == facilityId, consumeCancellationToken);

                                    if (existingRecord != null)
                                    {
                                        _logger.LogInformation("Facility {facilityId} found", facilityId);

                                        ScheduledReportEntity scheduledReport = _queryDispatchFactory.CreateScheduledReport(facilityId, value.ReportTypes, frequency, startDate, endDate, reportTrackingId);
                                        await scheduledReportMgr.UpdateScheduledReport(existingRecord, scheduledReport, consumeCancellationToken);
                                    }
                                    else
                                    {
                                        ScheduledReportEntity scheduledReport = _queryDispatchFactory.CreateScheduledReport(facilityId, value.ReportTypes, frequency, startDate, endDate, reportTrackingId);
                                        await scheduledReportMgr.createScheduledReport(scheduledReport, consumeCancellationToken);
                                    }

                                    accounted = true;
                                }
                                catch (DeadLetterException ex)
                                {
                                    if (consumeResult != null)
                                    {
                                        _deadLetterExceptionHandler.HandleException(consumeResult, ex, facilityId ?? string.Empty);
                                        accounted = true;
                                    }
                                }
                                catch (OperationCanceledException) when (consumeCancellationToken.IsCancellationRequested)
                                {
                                    throw;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Failed to process Report Scheduled event");

                                    var auditValue = new AuditEventMessage
                                    {
                                        FacilityId = facilityId,
                                        Action = AuditEventType.Query,
                                        ServiceName = QueryDispatchConstants.ServiceName,
                                        EventDate = DateTime.UtcNow,
                                        Notes = $"Report Scheduled event processing failure \nException Message: {ex}",
                                    };

                                    ProduceAuditEvent(auditValue, consumeResult?.Message?.Headers ?? new Headers());

                                    if (consumeResult != null)
                                    {
                                        _deadLetterExceptionHandler.HandleException(consumeResult, new DeadLetterException("Query Dispatch Exception thrown: " + ex.Message, ex), facilityId ?? string.Empty);
                                        accounted = true;
                                    }
                                }
                                finally
                                {
                                    if (accounted && consumeResult != null && !consumeCancellationToken.IsCancellationRequested)
                                    {
                                        assignmentTracker.MarkProcessed(consumeResult);
                                        _reportScheduledConsumer.SafeCommit(consumeResult, _logger);
                                    }
                                }

                            }, cancellationToken);
                        }
                        catch (ConsumeException ex)
                        {
                            _logger.LogError(ex, "Error consuming message for topics: [{Topics}] at {Timestamp}", string.Join(", ", _reportScheduledConsumer.Subscription), DateTime.UtcNow);

                            if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                            {
                                throw new OperationCanceledException(ex.Error.Reason, ex);
                            }

                            var facilityId = GetFacilityIdFromHeader(ex.ConsumerRecord?.Message?.Headers ?? new Headers());

                            _deadLetterExceptionHandler.HandleConsumeException(ex, facilityId);

                            var offset = ex.ConsumerRecord?.TopicPartitionOffset;
                            _reportScheduledConsumer.SafeCommit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset }, _logger);
                        }
                    }

                    _reportScheduledConsumer.Close();
                    _reportScheduledConsumer.Dispose();
                }
                catch (OperationCanceledException oce)
                {
                    _logger.LogError(oce, "Operation Canceled: {Message}", oce.Message);
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
                Key = KafkaKeys.ForAudit(
                    auditEvent.FacilityId,
                    string.IsNullOrWhiteSpace(auditEvent.FacilityId) ? null : auditEvent.PatientId,
                    QueryDispatchConstants.ServiceName),
                Value = auditEvent,
                Headers = headers
            });

        }
    }
}