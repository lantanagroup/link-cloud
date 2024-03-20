using Confluent.Kafka;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Application.ScheduledReport.Commands;
using LantanaGroup.Link.QueryDispatch.Application.ScheduledReport.Queries;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using System.Text;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.QueryDispatch.Listeners
{
    public class ReportScheduledEventListener : BackgroundService
    {
        private readonly ILogger<ReportScheduledEventListener> _logger;
        private readonly IKafkaConsumerFactory<ReportScheduledKey, ReportScheduledValue> _kafkaConsumerFactory;
        private readonly IQueryDispatchFactory _queryDispatchFactory;
        private readonly ICreateScheduledReportCommand _createScheduledReportCommand;
        private readonly IGetScheduledReportQuery _getScheduledReportQuery;
        private readonly IUpdateScheduledReportCommand _updateScheduledReportQuery;
        private readonly IKafkaProducerFactory<string, AuditEventMessage> _auditProducerFactory;

        public ReportScheduledEventListener(
            ILogger<ReportScheduledEventListener> logger,
            IKafkaConsumerFactory<ReportScheduledKey, ReportScheduledValue> kafkaConsumerFactory,
            IQueryDispatchFactory queryDispatchFactory, 
            ICreateScheduledReportCommand createScheduledReportCommand, 
            IGetScheduledReportQuery getReportScheduledQuery, 
            IUpdateScheduledReportCommand updateScheduledReportQuery,
            IKafkaProducerFactory<string, AuditEventMessage> auditProducer) 
        {
            _logger = logger;
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _queryDispatchFactory = queryDispatchFactory;
            _createScheduledReportCommand = createScheduledReportCommand;
            _getScheduledReportQuery = getReportScheduledQuery;
            _updateScheduledReportQuery = updateScheduledReportQuery;
            _auditProducerFactory = auditProducer;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async void StartConsumerLoop(CancellationToken cancellationToken) {

            var config = new ConsumerConfig() { 
                GroupId = "QueryDispatchReportScheduled",
                EnableAutoCommit = false
            };

            using (var _reportScheduledConsumer = _kafkaConsumerFactory.CreateConsumer(config))
            {
                try
                {
                    _reportScheduledConsumer.Subscribe(nameof(KafkaTopic.ReportScheduled));
                    _logger.LogInformation($"Started query dispatch consumer for topic '{KafkaTopic.ReportScheduled}' at {DateTime.UtcNow}");

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        ConsumeResult<ReportScheduledKey, ReportScheduledValue> consumeResult;
                        try 
                        {
                            consumeResult = _reportScheduledConsumer.Consume(cancellationToken);
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
                                Notes = $"Kafka ReportScheduled consume failure, potentially schema related \nException Message: {e.Error}",
                            };

                            ProduceAuditEvent(auditValue, e.ConsumerRecord.Message.Headers);

                            //TODO: We will eventually have Error/Retry workflows implemented
                            _reportScheduledConsumer.Commit();

                            continue;
                        }

                        try
                        {
                            string correlationId = string.Empty;

                            if (consumeResult.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                            {
                                correlationId = Encoding.UTF8.GetString(headerValue);
                            }
                            else
                            {
                                correlationId = Guid.NewGuid().ToString();
                            }

                            if (consumeResult != null)
                            {
                                ReportScheduledKey key = consumeResult.Message.Key;
                                ReportScheduledValue value = consumeResult.Message.Value;

                                DateTime startDate = new DateTime();
                                DateTime endDate = new DateTime();

                                var startDatePair = value.Parameters.Where(x => x.Key.ToLower() == "startdate").FirstOrDefault();
                                var endDatePair = value.Parameters.Where(x => x.Key.ToLower() == "enddate").FirstOrDefault();

                                if (startDatePair.Key == null || !DateTime.TryParse(startDatePair.Value, out startDate))
                                {
                                    _logger.LogError($"{key.ReportType} report start date is missing or improperly formatted for Facility {key.FacilityId}");
                                    continue;
                                }

                                if (endDatePair.Key == null || !DateTime.TryParse(endDatePair.Value, out endDate))
                                {
                                    _logger.LogError($"{key.ReportType} report end date is missing or improperly formatted for Facility {key.FacilityId}");
                                    continue;
                                }

                                _logger.LogInformation($"Consumed Event for: Facility '{key.FacilityId}' has a report type of '{key.ReportType}' with a report period of {startDate} to {endDate}");

                                var existingRecord = _getScheduledReportQuery.Execute(key.FacilityId);

                                if (existingRecord != null)
                                {
                                    _logger.LogInformation($"Facility {key.FacilityId} found");
                                    ScheduledReportEntity scheduledReport = _queryDispatchFactory.CreateScheduledReport(key.FacilityId, key.ReportType, startDate, endDate, correlationId);
                                    await _updateScheduledReportQuery.Execute(existingRecord, scheduledReport);
                                }
                                else
                                {
                                    ScheduledReportEntity scheduledReport = _queryDispatchFactory.CreateScheduledReport(key.FacilityId, key.ReportType, startDate, endDate, correlationId);
                                    await _createScheduledReportCommand.Execute(scheduledReport);
                                }

                                _reportScheduledConsumer.Commit(consumeResult);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Failed to process Report Scheduled event.", ex);

                            var auditValue = new AuditEventMessage
                            {
                                FacilityId = consumeResult.Message.Key.FacilityId,
                                Action = AuditEventType.Query,
                                ServiceName = "QueryDispatch",
                                EventDate = DateTime.UtcNow,
                                Notes = $"Report Scheduled event processing failure \nException Message: {ex}",
                            };

                            ProduceAuditEvent(auditValue, consumeResult.Message.Headers);

                            //TODO: We will eventually have Error/Retry workflows implemented
                            _reportScheduledConsumer.Commit();

                            continue;
                        }
                    }
                    _reportScheduledConsumer.Close();
                    _reportScheduledConsumer.Dispose();
                }
                catch (OperationCanceledException oce)
                {
                    _logger.LogError($"Operation Canceled: {oce.Message}", oce);
                    _reportScheduledConsumer.Close();
                    _reportScheduledConsumer.Dispose();
                }
            }
        }

        private void ProduceAuditEvent(AuditEventMessage auditEvent, Headers headers)
        {
            using (var producer = _auditProducerFactory.CreateAuditEventProducer())
            {
                producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                {
                    Value = auditEvent,
                    Headers = headers
                });
            }
        }
    }
}
