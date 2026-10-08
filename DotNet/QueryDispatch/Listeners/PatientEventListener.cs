using AngleSharp.Css.Dom;
using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using QueryDispatch.Application.Settings;
using QueryDispatch.Domain.Managers;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace LantanaGroup.Link.QueryDispatch.Listeners
{
    public class PatientEventListener : BackgroundService
    {
        private readonly ILogger<PatientEventListener> _logger;
        private readonly IKafkaConsumerFactory<string, PatientEventValue> _kafkaConsumerFactory;
        private readonly IQueryDispatchFactory _queryDispatchFactory;
        private readonly ITransientExceptionHandler<PatientEventListener, string, PatientEventValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<PatientEventListener, string, PatientEventValue> _deadLetterExceptionHandler;
        private readonly IDeadLetterExceptionHandler<PatientEventListener, string, string> _consumeResultDeadLetterExceptionHandler;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IProducer<string, AuditEventMessage> _producer;

        public PatientEventListener(
            ILogger<PatientEventListener> logger,
            IKafkaConsumerFactory<string, PatientEventValue> kafkaConsumerFactory,
            IQueryDispatchFactory queryDispatchFactory,
            IDeadLetterExceptionHandler<PatientEventListener, string, PatientEventValue> deadLetterExceptionHandler,
            IDeadLetterExceptionHandler<PatientEventListener, string, string> consumeResultDeadLetterExceptionHandler,
            ITransientExceptionHandler<PatientEventListener, string, PatientEventValue> transientExceptionHandler,
            IServiceScopeFactory serviceScopeFactory
,
            IProducer<string, AuditEventMessage> producer)
        {
            _logger = logger;
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _queryDispatchFactory = queryDispatchFactory;
            _serviceScopeFactory = serviceScopeFactory;
            _deadLetterExceptionHandler = deadLetterExceptionHandler;
            _transientExceptionHandler = transientExceptionHandler;
            _consumeResultDeadLetterExceptionHandler = consumeResultDeadLetterExceptionHandler;

            _transientExceptionHandler.Topic = nameof(KafkaTopic.PatientEvent) + "-Retry";

            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.PatientEvent) + "-Error";

            _consumeResultDeadLetterExceptionHandler.Topic = nameof(KafkaTopic.PatientEvent) + "-Error";
            _producer = producer ?? throw new ArgumentException(nameof(producer));
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
            using (var _patientEventConsumer = _kafkaConsumerFactory.CreateConsumer(config, assignmentTracker: assignmentTracker))
            {
                try
                {
                    _patientEventConsumer.Subscribe(KafkaTopicNames.Subscription(nameof(KafkaTopic.PatientEvent), QueryDispatchConstants.ServiceName));
                    _logger.LogInformation("Started query dispatch consumer for topic '{Topic}' at {DateTime}", KafkaTopic.PatientEvent, DateTime.UtcNow);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        ConsumeResult<string, PatientEventValue>? consumeResult = null;
                        try
                        {
                            await _patientEventConsumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                            {
                                consumeResult = result;
                                var accounted = false;
                                string? facilityId = null;
                                PatientEventValue? value = null;

                                try
                                {
                                    value = consumeResult?.Message?.Value;
                                    facilityId = KafkaIdentity.Facility(value?.FacilityId, consumeResult?.Message?.Key);
                                    if (consumeResult == null || value == null || string.IsNullOrWhiteSpace(facilityId) || !value.IsValid())
                                    {
                                        throw new DeadLetterException("Invalid Patient Event");
                                    }

                                    using var scope = _serviceScopeFactory.CreateScope();
                                    var patientDispatchMgr = scope.ServiceProvider.GetRequiredService<IPatientDispatchManager>();
                                    var scheduledReportRepository = scope.ServiceProvider.GetRequiredService<IBaseEntityRepository<ScheduledReportEntity>>();
                                    var queryDispatchConfigurationRepo = scope.ServiceProvider.GetRequiredService<IBaseEntityRepository<QueryDispatchConfigurationEntity>>();

                                    if (value.EventType != PatientEvents.Discharge.ToString())
                                    {
                                        _logger.LogInformation("Patient {PatientId} has event type of {EventType}. Ignoring.", HtmlInputSanitizer.Sanitize(value.PatientId), HtmlInputSanitizer.Sanitize(value.EventType));
                                        accounted = true;
                                        return;
                                    }

                                    string correlationId = string.Empty;

                                    if (consumeResult.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                                    {
                                        correlationId = System.Text.Encoding.UTF8.GetString(headerValue);
                                    }
                                    else
                                    {
                                        throw new DeadLetterException("Correlation Id missing");
                                    }

                                    _logger.LogInformation("Consumed Patient Event for: Facility '{FacilityId}'. PatientId '{PatientId}' with a event type of {EventType}", HtmlInputSanitizer.Sanitize(facilityId), HtmlInputSanitizer.Sanitize(value.PatientId), HtmlInputSanitizer.Sanitize(value.EventType));

                                    var scheduledReport = await scheduledReportRepository.FirstOrDefaultAsync(x => x.FacilityId == facilityId, consumeCancellationToken);

                                    if (scheduledReport == null)
                                    {
                                        throw new TransientException("PatientEventListener: scheduleReport is null.");
                                    }

                                    var now = DateTime.UtcNow;
                                    scheduledReport.ReportPeriods = scheduledReport.ReportPeriods.Where(r => r.StartDate <= now && r.EndDate >= now).ToList();

                                    QueryDispatchConfigurationEntity dispatchSchedule = await queryDispatchConfigurationRepo.FirstOrDefaultAsync(x => x.FacilityId == facilityId, consumeCancellationToken);

                                    if (dispatchSchedule == null)
                                    {
                                        throw new TransientException($"Query dispatch configuration missing for facility {HtmlInputSanitizer.Sanitize(facilityId)}");
                                    }

                                    DispatchSchedule dischargeDispatchSchedule = dispatchSchedule.DispatchSchedules.FirstOrDefault(x => x.Event == QueryDispatchConstants.EventType.Discharge);

                                    if (dischargeDispatchSchedule == null)
                                    {
                                        throw new TransientException($"'Discharge' query dispatch configuration missing for facility {HtmlInputSanitizer.Sanitize(facilityId)}");
                                    }

                                    PatientDispatchEntity patientDispatch = _queryDispatchFactory.CreatePatientDispatch(facilityId, value.PatientId, value.EventType, correlationId, scheduledReport, dischargeDispatchSchedule);

                                    if (patientDispatch.ScheduledReportPeriods == null || patientDispatch.ScheduledReportPeriods.Count == 0)
                                    {
                                        throw new TransientException($"No active scheduled report periods found for facility {HtmlInputSanitizer.Sanitize(facilityId)}");
                                    }

                                    await patientDispatchMgr.createPatientDispatch(patientDispatch, consumeCancellationToken);
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
                                catch (TransientException ex)
                                {
                                    if (consumeResult != null)
                                    {
                                        _transientExceptionHandler.HandleException(consumeResult, ex, facilityId ?? string.Empty);
                                        accounted = true;
                                    }
                                }
                                catch (OperationCanceledException) when (consumeCancellationToken.IsCancellationRequested)
                                {
                                    throw;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Failed to process Patient Event");

                                    var auditValue = new AuditEventMessage
                                    {
                                        FacilityId = facilityId,
                                        PatientId = value?.PatientId,
                                        Action = AuditEventType.Query,
                                        ServiceName = QueryDispatchConstants.ServiceName,
                                        EventDate = DateTime.UtcNow,
                                        Notes = $"Patient Event processing failure \nException Message: {ex}",
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
                                        _patientEventConsumer.SafeCommit(consumeResult, _logger);
                                    }
                                }

                            }, cancellationToken);
                        }
                        catch (ConsumeException e)
                        {
                            if (e.Error.Code == ErrorCode.UnknownTopicOrPart)
                            {
                                throw new OperationCanceledException(e.Error.Reason, e);
                            }

                            var rawKey = e.ConsumerRecord?.Message?.Key != null ? Encoding.UTF8.GetString(e.ConsumerRecord.Message.Key) : null;
                            var facilityId = KafkaIdentity.Facility(null, rawKey) ?? string.Empty;

                            _consumeResultDeadLetterExceptionHandler.HandleConsumeException(e, facilityId);

                            var offset = e.ConsumerRecord?.TopicPartitionOffset;
                            _patientEventConsumer.SafeCommit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset }, _logger);
                        }
                    }
                    _patientEventConsumer.Close();
                    _patientEventConsumer.Dispose();
                }
                catch (OperationCanceledException oce)
                {
                    _logger.LogError(oce, "Operation Canceled: {Message}", oce.Message);
                    _patientEventConsumer.Close();
                    _patientEventConsumer.Dispose();
                }
            }
        }

        private void ProduceAuditEvent(AuditEventMessage auditValue, Headers headers)
        {

            _producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
            {
                Key = KafkaKeys.ForAudit(
                    auditValue.FacilityId,
                    string.IsNullOrWhiteSpace(auditValue.FacilityId) ? null : auditValue.PatientId,
                    QueryDispatchConstants.ServiceName),
                Value = auditValue,
                Headers = headers
            });

        }
    }
}
