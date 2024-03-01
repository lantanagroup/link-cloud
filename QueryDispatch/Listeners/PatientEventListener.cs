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

        public PatientEventListener(ILogger<PatientEventListener> logger, IKafkaConsumerFactory<string, PatientEventValue> kafkaConsumerFactory, IQueryDispatchFactory queryDispatchFactory, ICreatePatientDispatchCommand createPatientDispatchCommand, IGetScheduledReportQuery getScheduledReportQuery, IGetQueryDispatchConfigurationQuery getQueryDispatchConfigurationQuery, IKafkaProducerFactory<string, AuditEventMessage> auditProducerFactory) 
        {
            _logger = logger;
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _queryDispatchFactory = queryDispatchFactory;
            _createPatientDispatchCommand = createPatientDispatchCommand;
            _getScheduledReportQuery = getScheduledReportQuery;
            _getQueryDispatchConfigurationQuery = getQueryDispatchConfigurationQuery;
            _auditProducerFactory = auditProducerFactory;
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
                        ConsumeResult<string, PatientEventValue> consumeResult;
                        try
                        {
                            consumeResult = _patientEventConsumer.Consume(cancellationToken);
                        }
                        catch (ConsumeException e)
                        {
                            _logger.LogError($"Consume failure, potentially schema related: {e.Error.Reason}");
                            var potentialFacilityId = Encoding.UTF8.GetString(e.ConsumerRecord.Message.Key);

                            var auditValue = new AuditEventMessage
                            {
                                FacilityId = potentialFacilityId,
                                Action = AuditEventType.Query,
                                ServiceName = "QueryDispatch",
                                EventDate = DateTime.UtcNow,
                                Notes = $"Kafka PatientEvent consume failure, potentially schema related \nException Message: {e.Error}",
                            };

                            ProduceAuditEvent(auditValue, e.ConsumerRecord.Message.Headers);

                            //TODO: We will eventually have Error/Retry workflows implemented
                            _patientEventConsumer.Commit();

                            continue;
                        }

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
                                correlationId = Guid.NewGuid().ToString();
                            }

                            _logger.LogInformation($"Consumed Patient Event for: Facility '{consumeResult.Message.Key}'. PatientId '{value.PatientId}' with a event type of {value.EventType}");

                            ScheduledReportEntity scheduledReport = _getScheduledReportQuery.Execute(consumeResult.Message.Key);

                            QueryDispatchConfigurationEntity dispatchSchedule = await _getQueryDispatchConfigurationQuery.Execute(consumeResult.Message.Key);

                            if (dispatchSchedule == null)
                            {
                                throw new Exception($"Query dispatch configuration missing for facility {consumeResult.Message.Key}");
                            }
                                
                            DispatchSchedule dischargeDispatchSchedule = dispatchSchedule.DispatchSchedules.FirstOrDefault(x => x.Event == QueryDispatchConstants.EventType.Discharge);

                            if (dischargeDispatchSchedule == null)
                            {
                                throw new Exception($"'Discharge' query dispatch configuration missing for facility {consumeResult.Message.Key}");
                            }
                                
                            PatientDispatchEntity patientDispatch = _queryDispatchFactory.CreatePatientDispatch(consumeResult.Message.Key, value.PatientId, value.EventType, correlationId, scheduledReport, dischargeDispatchSchedule);

                            if (patientDispatch.ScheduledReportPeriods == null || patientDispatch.ScheduledReportPeriods.Count == 0)
                            { 
                                //TODO - Daniel: We will eventually have Error/Retry workflows implemented
                                throw new Exception($"No active scheduled report periods found for facility {consumeResult.Message.Key}");
                            }
                                
                            await _createPatientDispatchCommand.Execute(patientDispatch, dispatchSchedule);

                            _patientEventConsumer.Commit(consumeResult);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Failed to process Patient Event.", ex);

                            var auditValue = new AuditEventMessage
                            {
                                FacilityId = consumeResult.Message.Key,
                                Action = AuditEventType.Query,
                                ServiceName = "QueryDispatch",
                                EventDate = DateTime.UtcNow,
                                Notes = $"Patient Event processing failure \nException Message: {ex}",
                            };

                            ProduceAuditEvent(auditValue, consumeResult.Message.Headers);

                            //TODO: We will eventually have Error/Retry workflows implemented
                            _patientEventConsumer.Commit();

                            continue;
                        }
                    }
                    _patientEventConsumer.Close();
                    _patientEventConsumer.Dispose();
                }
                catch (OperationCanceledException oce)
                {
                    _logger.LogError($"Operation Canceled: {oce.Message}", oce);
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
