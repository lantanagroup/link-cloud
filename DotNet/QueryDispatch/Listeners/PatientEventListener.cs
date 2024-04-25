using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using Confluent.Kafka;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.QueryDispatch.Application.PatientDispatch.Commands;
using LantanaGroup.Link.QueryDispatch.Application.ScheduledReport.Queries;
using LantanaGroup.Link.QueryDispatch.Application.Queries;
using LantanaGroup.Link.Shared.Application.Interfaces;
using System.Text;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using QueryDispatch.Application.Settings;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using Confluent.Kafka.Extensions.Diagnostics;

namespace LantanaGroup.Link.QueryDispatch.Listeners
{
    public class PatientEventListener : BackgroundService
    {
        private readonly ILogger<PatientEventListener> _logger;
        private readonly IKafkaConsumerFactory<string, PatientEventValue> _kafkaConsumerFactory;
        private readonly IQueryDispatchFactory _queryDispatchFactory;
        private readonly ICreatePatientDispatchCommand _createPatientDispatchCommand;
        private readonly IGetScheduledReportQuery _getScheduledReportQuery;
        private readonly IGetQueryDispatchConfigurationQuery _getQueryDispatchConfigurationQuery;
        private readonly IKafkaProducerFactory<string, AuditEventMessage> _auditProducerFactory;
        private readonly ITransientExceptionHandler<string, PatientEventValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<string, PatientEventValue> _deadLetterExceptionHandler;
        private readonly IDeadLetterExceptionHandler<string, string> _consumeResultDeadLetterExceptionHandler;

        public PatientEventListener(ILogger<PatientEventListener> logger, IKafkaConsumerFactory<string, PatientEventValue> kafkaConsumerFactory, IQueryDispatchFactory queryDispatchFactory, ICreatePatientDispatchCommand createPatientDispatchCommand, IGetScheduledReportQuery getScheduledReportQuery, IGetQueryDispatchConfigurationQuery getQueryDispatchConfigurationQuery, IKafkaProducerFactory<string, AuditEventMessage> auditProducerFactory, IDeadLetterExceptionHandler<string, PatientEventValue> deadLetterExceptionHandler, IDeadLetterExceptionHandler<string, string> consumeResultDeadLetterExceptionHandler, ITransientExceptionHandler<string, PatientEventValue> transientExceptionHandler) 
        {
            _logger = logger;
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _queryDispatchFactory = queryDispatchFactory;
            _createPatientDispatchCommand = createPatientDispatchCommand;
            _getScheduledReportQuery = getScheduledReportQuery;
            _getQueryDispatchConfigurationQuery = getQueryDispatchConfigurationQuery;
            _auditProducerFactory = auditProducerFactory;
            _deadLetterExceptionHandler = deadLetterExceptionHandler;
            _transientExceptionHandler = transientExceptionHandler;
            _consumeResultDeadLetterExceptionHandler = consumeResultDeadLetterExceptionHandler;

            _transientExceptionHandler.ServiceName = "QueryDispatch";
            _transientExceptionHandler.Topic = nameof(KafkaTopic.PatientEvent) + "-Retry";

            _deadLetterExceptionHandler.ServiceName = "QueryDispatch";
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.PatientEvent) + "-Error";

            _consumeResultDeadLetterExceptionHandler.ServiceName = "QueryDispatch";
            _consumeResultDeadLetterExceptionHandler.Topic = nameof(KafkaTopic.PatientEvent) + "-Error";
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async void StartConsumerLoop(CancellationToken cancellationToken) {
            var config = new ConsumerConfig()
            {
                GroupId = "QueryDispatchPatientEvent",
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
                                    PatientEventValue value = consumeResult.Message.Value;
                                    string correlationId = string.Empty;

                                    if (consumeResult.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                                    {
                                        correlationId = System.Text.Encoding.UTF8.GetString(headerValue);
                                    }
                                    else
                                    {
                                        throw new DeadLetterException("Correlation Id missing", AuditEventType.Create);
                                    }

                                    _logger.LogInformation($"Consumed Patient Event for: Facility '{consumeResult.Message.Key}'. PatientId '{value.PatientId}' with a event type of {value.EventType}");

                                    ScheduledReportEntity scheduledReport = _getScheduledReportQuery.Execute(consumeResult.Message.Key);

                                    QueryDispatchConfigurationEntity dispatchSchedule = await _getQueryDispatchConfigurationQuery.Execute(consumeResult.Message.Key);

                                    if (dispatchSchedule == null)
                                    {
                                        throw new TransientException($"Query dispatch configuration missing for facility {consumeResult.Message.Key}", AuditEventType.Query);
                                    }

                                    DispatchSchedule dischargeDispatchSchedule = dispatchSchedule.DispatchSchedules.FirstOrDefault(x => x.Event == QueryDispatchConstants.EventType.Discharge);

                                    if (dischargeDispatchSchedule == null)
                                    {
                                        throw new TransientException($"'Discharge' query dispatch configuration missing for facility {consumeResult.Message.Key}", AuditEventType.Query);
                                    }

                                    PatientDispatchEntity patientDispatch = _queryDispatchFactory.CreatePatientDispatch(consumeResult.Message.Key, value.PatientId, value.EventType, correlationId, scheduledReport, dischargeDispatchSchedule);

                                    if (patientDispatch.ScheduledReportPeriods == null || patientDispatch.ScheduledReportPeriods.Count == 0)
                                    {
                                        throw new TransientException($"No active scheduled report periods found for facility {consumeResult.Message.Key}", AuditEventType.Query);
                                    }

                                    await _createPatientDispatchCommand.Execute(patientDispatch, dispatchSchedule);

                                    _patientEventConsumer.Commit(consumeResult);
                                }
                                catch (DeadLetterException ex)
                                {
                                    _deadLetterExceptionHandler.HandleException(consumeResult, ex, consumeResult.Key);
                                    _patientEventConsumer.Commit(consumeResult);
                                }
                                catch (TransientException ex)
                                {
                                    _transientExceptionHandler.HandleException(consumeResult, ex, consumeResult.Key);
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

                                    _deadLetterExceptionHandler.HandleException(consumeResult, new DeadLetterException("Query Dispatch Exception thrown: " + ex.Message, AuditEventType.Create), consumeResult.Message.Key);
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

                            var facilityId = Encoding.UTF8.GetString(e.ConsumerRecord.Message.Key);
                            var converted_record = new ConsumeResult<string, string>()
                            {
                                Message = new Message<string, string>()
                                {
                                    Key = facilityId,
                                    Value = Encoding.UTF8.GetString(e.ConsumerRecord.Message.Value),
                                    Headers = e.ConsumerRecord.Message.Headers
                                }
                            };

                            _consumeResultDeadLetterExceptionHandler.HandleException(converted_record, new DeadLetterException("Consume Result exception: " + e.InnerException.Message, AuditEventType.Create), facilityId);

                            _patientEventConsumer.Commit();
                            continue;
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
            using (var producer = _auditProducerFactory.CreateAuditEventProducer())
            {
                producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                {
                    Value = auditValue,
                    Headers = headers
                });
            }
        }
    }
}
