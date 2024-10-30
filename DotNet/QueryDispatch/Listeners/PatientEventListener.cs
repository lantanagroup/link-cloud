using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Application.Services.Security;
using QueryDispatch.Application.Settings;
using QueryDispatch.Domain.Managers;
using System.Text;

namespace LantanaGroup.Link.QueryDispatch.Listeners
{
    public class PatientEventListener : BackgroundService
    {
        private readonly ILogger<PatientEventListener> _logger;
        private readonly IKafkaConsumerFactory<string, PatientEventValue> _kafkaConsumerFactory;
        private readonly IQueryDispatchFactory _queryDispatchFactory;
        private readonly ITransientExceptionHandler<string, PatientEventValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<string, PatientEventValue> _deadLetterExceptionHandler;
        private readonly IDeadLetterExceptionHandler<string, string> _consumeResultDeadLetterExceptionHandler;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IProducer<string, AuditEventMessage> _producer;

        public PatientEventListener(
            ILogger<PatientEventListener> logger,
            IKafkaConsumerFactory<string, PatientEventValue> kafkaConsumerFactory,
            IQueryDispatchFactory queryDispatchFactory,
            IDeadLetterExceptionHandler<string, PatientEventValue> deadLetterExceptionHandler,
            IDeadLetterExceptionHandler<string, string> consumeResultDeadLetterExceptionHandler,
            ITransientExceptionHandler<string, PatientEventValue> transientExceptionHandler,
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

            _transientExceptionHandler.ServiceName = "QueryDispatch";
            _transientExceptionHandler.Topic = nameof(KafkaTopic.PatientEvent) + "-Retry";

            _deadLetterExceptionHandler.ServiceName = "QueryDispatch";
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.PatientEvent) + "-Error";

            _consumeResultDeadLetterExceptionHandler.ServiceName = "QueryDispatch";
            _consumeResultDeadLetterExceptionHandler.Topic = nameof(KafkaTopic.PatientEvent) + "-Error";
            _producer = producer ?? throw new ArgumentException(nameof(producer));
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async void StartConsumerLoop(CancellationToken cancellationToken) {
            var config = new ConsumerConfig()
            {
                GroupId = QueryDispatchConstants.ServiceName,
                EnableAutoCommit = false
            };

            using (var _patientEventConsumer = _kafkaConsumerFactory.CreateConsumer(config)) {
                try
                {
                    _patientEventConsumer.Subscribe(nameof(KafkaTopic.PatientEvent));
                    _logger.LogInformation($"Started query dispatch consumer for topic '{KafkaTopic.PatientEvent}' at {DateTime.UtcNow}");

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        ConsumeResult<string, PatientEventValue>? consumeResult;
                        try
                        {
                            await _patientEventConsumer.ConsumeWithInstrumentation(async (result, cancellationToken) =>
                            {
                                consumeResult = result;

                                try
                                {
                                    using var scope = _serviceScopeFactory.CreateScope();
                                    var patientDispatchMgr = scope.ServiceProvider.GetRequiredService<IPatientDispatchManager>();
                                    var scheduledReportRepository = scope.ServiceProvider.GetRequiredService<IEntityRepository<ScheduledReportEntity>>();
                                    var queryDispatchConfigurationRepo = scope.ServiceProvider.GetRequiredService<IEntityRepository<QueryDispatchConfigurationEntity>>();

                                    if (consumeResult == null || consumeResult.Key == null || !consumeResult.Value.IsValid())
                                    {
                                        throw new DeadLetterException("Invalid Patient Event");
                                    }

                                    PatientEventValue value = consumeResult.Message.Value;
                                    string correlationId = string.Empty;

                                    if (consumeResult.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                                    {
                                        correlationId = System.Text.Encoding.UTF8.GetString(headerValue);
                                    }
                                    else
                                    {
                                        throw new DeadLetterException("Correlation Id missing");
                                    }

                                    _logger.LogInformation($"Consumed Patient Event for: Facility '{HtmlInputSanitizer.Sanitize(consumeResult.Message.Key)}'. PatientId '{HtmlInputSanitizer.Sanitize(value.PatientId)}' with a event type of {HtmlInputSanitizer.Sanitize(value.EventType)}");

                                    //ScheduledReportEntity scheduledReport = getScheduledReportQuery.Execute(consumeResult.Message.Key);
                                    ScheduledReportEntity scheduledReport  =  await scheduledReportRepository.FirstOrDefaultAsync(x => x.FacilityId == consumeResult.Message.Key);

                                    if (scheduledReport == null)
                                    {
                                       throw new TransientException("PatientEventListener: scheduleReport is null.");
                                    }

                                    var now = DateTime.UtcNow;
                                    scheduledReport.ReportPeriods = scheduledReport.ReportPeriods.Where(r => r.StartDate <= now && r.EndDate >= now).ToList();

                                    // QueryDispatchConfigurationEntity dispatchSchedule = await queryDispatchConfigurationQuery.Execute(consumeResult.Message.Key);
                                    QueryDispatchConfigurationEntity dispatchSchedule= await queryDispatchConfigurationRepo.FirstOrDefaultAsync(x => x.FacilityId == consumeResult.Message.Key);

                                    if (dispatchSchedule == null)
                                    {
                                        throw new TransientException($"Query dispatch configuration missing for facility {HtmlInputSanitizer.Sanitize(consumeResult.Message.Key)}");
                                    }

                                    DispatchSchedule dischargeDispatchSchedule = dispatchSchedule.DispatchSchedules.FirstOrDefault(x => x.Event == QueryDispatchConstants.EventType.Discharge);

                                    if (dischargeDispatchSchedule == null)
                                    {
                                        throw new TransientException($"'Discharge' query dispatch configuration missing for facility {HtmlInputSanitizer.Sanitize(consumeResult.Message.Key)}");
                                    }

                                    PatientDispatchEntity patientDispatch = _queryDispatchFactory.CreatePatientDispatch(consumeResult.Message.Key, value.PatientId, value.EventType, correlationId, scheduledReport, dischargeDispatchSchedule);

                                    if (patientDispatch.ScheduledReportPeriods == null || patientDispatch.ScheduledReportPeriods.Count == 0)
                                    {
                                        throw new TransientException($"No active scheduled report periods found for facility {HtmlInputSanitizer.Sanitize(consumeResult.Message.Key)}");
                                    }

                                    await patientDispatchMgr.createPatientDispatch(patientDispatch);

                                    _patientEventConsumer.Commit(consumeResult);
                                }
                                catch (DeadLetterException ex)
                                {
                                    _deadLetterExceptionHandler.HandleException(consumeResult, ex, HtmlInputSanitizer.Sanitize(consumeResult.Key));
                                    _patientEventConsumer.Commit(consumeResult);
                                }
                                catch (TransientException ex)
                                {
                                    _transientExceptionHandler.HandleException(consumeResult, ex, HtmlInputSanitizer.Sanitize(consumeResult.Key));
                                    _patientEventConsumer.Commit(consumeResult);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, $"Failed to process Patient Event.");

                                    var auditValue = new AuditEventMessage
                                    {
                                        FacilityId = consumeResult.Message.Key,
                                        Action = AuditEventType.Query,
                                        ServiceName = "QueryDispatch",
                                        EventDate = DateTime.UtcNow,
                                        Notes = $"Patient Event processing failure \nException Message: {ex}",
                                    };

                                    ProduceAuditEvent(auditValue, consumeResult.Message.Headers);

                                    _deadLetterExceptionHandler.HandleException(consumeResult, new DeadLetterException("Query Dispatch Exception thrown: " + ex.Message), consumeResult.Message.Key);
                                    _patientEventConsumer.Commit();

                                    //continue;
                                }

                            }, cancellationToken);
                        }
                        catch (ConsumeException e)
                        {
                            if (e.Error.Code == ErrorCode.UnknownTopicOrPart)
                            {
                                throw new OperationCanceledException(e.Error.Reason, e);
                            }

                            var facilityId = e.ConsumerRecord.Message.Key != null ? Encoding.UTF8.GetString(e.ConsumerRecord.Message.Key) : "";

                            _consumeResultDeadLetterExceptionHandler.HandleConsumeException(e, facilityId);

                            _patientEventConsumer.Commit();
                        }                        
                    }
                    _patientEventConsumer.Close();
                    _patientEventConsumer.Dispose();
                }
                catch (OperationCanceledException oce)
                {
                    _logger.LogError(oce, $"Operation Canceled: {oce.Message}");
                    _patientEventConsumer.Close();
                    _patientEventConsumer.Dispose();
                }
            }
        }

        private void ProduceAuditEvent(AuditEventMessage auditValue, Headers headers)
        {

                _producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                {
                    Value = auditValue,
                    Headers = headers
                });
            
        }
    }
}
