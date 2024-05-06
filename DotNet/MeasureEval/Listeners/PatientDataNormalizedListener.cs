using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using LantanaGroup.Link.MeasureEval.Auditing;
using LantanaGroup.Link.MeasureEval.Models;
using LantanaGroup.Link.MeasureEval.Services;
using LantanaGroup.Link.MeasureEval.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.MeasureEval.Listeners
{
    public class PatientDataNormalizedListener : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly IKafkaConsumerFactory<string, PatientDataNormalizedMessage> _kafkaConsumerFactory;
        private readonly IKafkaProducerFactory<PatientDataEvaluatedKey, PatientDataEvaluatedMessage> _kafkaProducerFactory;
        private readonly IKafkaProducerFactory<string, AuditEventMessage> _kafkaAuditProducerFactory;
        private readonly IMeasureEvalReportService _measureEvalReportService;


        public PatientDataNormalizedListener(ILogger<PatientDataNormalizedListener> logger,
            IKafkaProducerFactory<PatientDataEvaluatedKey, PatientDataEvaluatedMessage> kafkaProducerFactory,
            IKafkaProducerFactory<string, AuditEventMessage> kafkaAuditProducerFactory,
            IKafkaConsumerFactory<string, PatientDataNormalizedMessage> kafkaConsumerFactory,
            IMeasureEvalReportService measureEvalReportService)
        {
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
            this._kafkaAuditProducerFactory = kafkaAuditProducerFactory ?? throw new ArgumentNullException(nameof(kafkaAuditProducerFactory));
            this._kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentNullException(nameof(kafkaConsumerFactory));
            this._measureEvalReportService = measureEvalReportService ?? throw new ArgumentNullException(nameof(measureEvalReportService));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var config = new ConsumerConfig
            {
                GroupId = MeasureEvalConstants.ServiceName,              
                EnableAutoCommit = false,
            };

            using (var consumer = _kafkaConsumerFactory.CreateConsumer(config)) 
            {
                try
                {
                    consumer.Subscribe(nameof(KafkaTopic.PatientNormalized));
                    _logger.LogTrace($"Subscribed to topic {KafkaTopic.PatientNormalized}");

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        ConsumeResult<string, PatientDataNormalizedMessage>? consumeResult;

                        await consumer.ConsumeWithInstrumentation(async (result, cancellationToken) =>
                        {
                            consumeResult = result;

                            if (consumeResult != null)
                            {
                                PatientDataNormalizedMessage patientMessage = consumeResult.Message.Value;
                                string CorrelationId = "";

                                if (consumeResult.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                                {
                                    CorrelationId = System.Text.Encoding.UTF8.GetString(headerValue);
                                    _logger.LogInformation($"Received message with correlation ID {CorrelationId}: {consumeResult.Topic}");
                                }
                                else
                                {
                                    _logger.LogInformation($"Received message without correlation ID: {consumeResult.Topic}");
                                }

                                _logger.LogTrace($"Consuming {KafkaTopic.PatientNormalized} message");

                                MeasureReport? measureReport = await _measureEvalReportService.EvaluateAsync(consumeResult.Message.Key, patientMessage, CorrelationId, stoppingToken);

                                var headers = new Headers();
                                string correlationId = (!string.IsNullOrEmpty(CorrelationId) ? new Guid(CorrelationId).ToString() : Guid.NewGuid().ToString());

                                headers.Add("X-Correlation-Id", System.Text.Encoding.ASCII.GetBytes(correlationId));
                                Bundle patientBundle = (Bundle)patientMessage.PatientBundle;

                                if (measureReport != null)
                                {
                                    //generate an ID
                                    measureReport.Id = Guid.NewGuid().ToString();

                                    using (var producer = _kafkaProducerFactory.CreateProducer(new ProducerConfig())) 
                                    {
                                        producer.Produce(
                                            KafkaTopic.MeasureEvaluated.ToString(), 
                                            new Message<PatientDataEvaluatedKey, PatientDataEvaluatedMessage>()
                                            {
                                                Key = new PatientDataEvaluatedKey
                                                {
                                                    //TODO: Account for additional list entries
                                                    FacilityId = consumeResult.Message.Key,
                                                    StartDate = patientMessage.ScheduledReports.First<ScheduledReport>().StartDate,
                                                    EndDate = patientMessage.ScheduledReports.First<ScheduledReport>().EndDate,
                                                    ReportType = patientMessage.ScheduledReports.First<ScheduledReport>().ReportType

                                                },
                                                Value = new PatientDataEvaluatedMessage
                                                {
                                                    PatientId = patientMessage.PatientId,
                                                    Result = measureReport
                                                },
                                                Headers = headers
                                            });
                                    }

                                    AuditEventMessage auditEvent = new AuditEventMessage();
                                    auditEvent.ServiceName = "MeasureEvalReportService";
                                    auditEvent.EventDate = DateTime.UtcNow;
                                    auditEvent.User = "SystemUser";
                                    auditEvent.Action = AuditEventType.Create;
                                    auditEvent.Resource = "Patient: " + patientBundle.Id + " Measure: " + patientMessage.ScheduledReports.First<ScheduledReport>().ReportType + " Measure Report Id: " + measureReport.Id;
                                    auditEvent.Notes = $"Report Message created for {patientMessage.PatientId} and Measure: {patientMessage.ScheduledReports.First<ScheduledReport>().ReportType} and Measure Report Id: {measureReport.Id}";


                                    using (var producer = _kafkaAuditProducerFactory.CreateProducer(new ProducerConfig()))
                                    {
                                        producer.Produce(
                                            KafkaTopic.AuditableEventOccurred.ToString(),
                                            new Message<string, AuditEventMessage>()
                                            {
                                                Key = consumeResult.Message.Key,
                                                Value = auditEvent,
                                                Headers = headers
                                            });
                                    }
                                }
                                else
                                {
                                    _logger.LogError($"Facility ID {consumeResult.Message.Key}'s evaluation for patient {patientMessage.PatientId} did not result in a MeasureReport");
                                }
                            }
                        }, stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                  
                }
            }
        }
    }
}
