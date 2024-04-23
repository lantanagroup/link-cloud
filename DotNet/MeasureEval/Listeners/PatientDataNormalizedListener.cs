using Confluent.Kafka;
using Hl7.Fhir.Model;
using LantanaGroup.Link.MeasureEval.Models;
using LantanaGroup.Link.MeasureEval.Services;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Wrappers;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.MeasureEval.Listeners
{
    public class PatientDataNormalizedListener : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly IKafkaWrapper<string, PatientDataNormalizedMessage, PatientDataEvaluatedKey, PatientDataEvaluatedMessage> _kafkaWrapper;
        private readonly IKafkaWrapper<Ignore, Null, string, AuditEventMessage> _kafkaAuditWrapper;
        private readonly IMeasureEvalReportService _measureEvalReportService;


        public PatientDataNormalizedListener(ILogger<PatientDataNormalizedListener> logger,
            IKafkaWrapper<string, PatientDataNormalizedMessage, PatientDataEvaluatedKey, PatientDataEvaluatedMessage> kafkaWrapper,
            IKafkaWrapper<Ignore, Null, string, AuditEventMessage> kafkaAuditWrapper,
            IMeasureEvalReportService measureEvalReportService)
        {
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._kafkaWrapper = kafkaWrapper ?? throw new ArgumentNullException(nameof(kafkaWrapper));
            this._kafkaAuditWrapper = kafkaAuditWrapper ?? throw new ArgumentNullException(nameof(kafkaAuditWrapper));
            this._measureEvalReportService = measureEvalReportService ?? throw new ArgumentNullException(nameof(measureEvalReportService));
        }

        public override void Dispose()
        {
            base.Dispose();

            _kafkaWrapper.CloseConsumer();
            _kafkaWrapper.DisposeConsumer();

            _kafkaWrapper.DisposeProducer();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _kafkaWrapper.SubscribeToKafkaTopic(new string[] { KafkaTopic.PatientNormalized.ToString() });
            _logger.LogTrace($"Subscribed to topic {KafkaTopic.PatientNormalized}");

            while (true)
            {
                try
                {
                    var consumeResult = await Task.Run(() => _kafkaWrapper.ConsumeAndReturnFullMessage(stoppingToken), stoppingToken);
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
                            await _kafkaWrapper.ProduceKafkaMessageAsync(KafkaTopic.MeasureEvaluated.ToString(), () => new Message<PatientDataEvaluatedKey, PatientDataEvaluatedMessage>
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

                            AuditEventMessage auditEvent = new AuditEventMessage();
                            auditEvent.ServiceName = "MeasureEvalReportService";
                            auditEvent.EventDate = DateTime.UtcNow;
                            auditEvent.User = "SystemUser";
                            auditEvent.Action = AuditEventType.Create;
                            auditEvent.Resource = "Patient: " + patientBundle.Id + " Measure: " + patientMessage.ScheduledReports.First<ScheduledReport>().ReportType + " Measure Report Id: " + measureReport.Id;
                            auditEvent.Notes = $"Report Message created for {patientMessage.PatientId} and Measure: {patientMessage.ScheduledReports.First<ScheduledReport>().ReportType} and Measure Report Id: {measureReport.Id}";


                            await this._kafkaAuditWrapper.ProduceKafkaMessageAsync(KafkaTopic.AuditableEventOccurred.ToString(), () =>
                            {
                                return new Message<string, AuditEventMessage>
                                {
                                    Key = consumeResult.Message.Key,
                                    Value = auditEvent,
                                    Headers = headers
                                };
                            });

                        }
                        else
                        {
                            _logger.LogError($"Facility ID {consumeResult.Message.Key}'s evaluation for patient {patientMessage.PatientId} did not result in a MeasureReport");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
