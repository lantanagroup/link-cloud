using Confluent.Kafka;
using LantanaGroup.Link.QueryDispatch;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Application.PatientDispatch.Commands;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Interfaces;
using Quartz;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using System.Text;
using QueryDispatch.Application.Settings;

namespace LanatanGroup.Link.QueryDispatch.Jobs
{
    [DisallowConcurrentExecution]
    public class QueryDispatchJob : IJob
    {
        private readonly ILogger<QueryDispatchJob> _logger;
        private readonly IKafkaProducerFactory<string, DataAcquisitionRequestedValue> _acquisitionProducerFactory;
        private readonly IKafkaProducerFactory<string, AuditEventMessage> _auditProducerFactory;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public QueryDispatchJob(
            ILogger<QueryDispatchJob> logger, 
            IKafkaProducerFactory<string, DataAcquisitionRequestedValue> acquisitionProducerFactory,
            IKafkaProducerFactory<string, AuditEventMessage> auditProducerFactory, 
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _acquisitionProducerFactory = acquisitionProducerFactory ?? throw new ArgumentNullException(nameof(acquisitionProducerFactory));
            _auditProducerFactory = auditProducerFactory ?? throw new ArgumentNullException(nameof(auditProducerFactory));
            _serviceScopeFactory = serviceScopeFactory;
        }

        public Task Execute(IJobExecutionContext context)
        {
            JobDataMap triggerMap = context.Trigger.JobDataMap!;
            PatientDispatchEntity patientDispatchEntity = (PatientDispatchEntity)triggerMap["PatientDispatchEntity"];

            ProducerConfig config = new ProducerConfig()
            {
                ClientId = "QueryDispatch_DataAcquisitionScheduled"
            };

            using (var producer = _acquisitionProducerFactory.CreateProducer(config))
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var _deletePatientDispatchCommand = scope.ServiceProvider.GetRequiredService<IDeletePatientDispatchCommand>();

                    DataAcquisitionRequestedValue dataAcquisitionRequestedValue = new DataAcquisitionRequestedValue()
                    {
                        PatientId = patientDispatchEntity.PatientId,
                        ScheduledReports = new List<ScheduledReport>(),
                        QueryType = QueryTypes.Initial.ToString()
                    };

                    foreach (var scheduledReportPeriod in patientDispatchEntity.ScheduledReportPeriods)
                    {
                        dataAcquisitionRequestedValue.ScheduledReports.Add(new ScheduledReport
                        {
                            ReportType = scheduledReportPeriod.ReportType,
                            StartDate = scheduledReportPeriod.StartDate,
                            EndDate = scheduledReportPeriod.EndDate
                        });
                    }

                    var headers = new Headers
                    {
                        { "X-Correlation-Id", Encoding.UTF8.GetBytes(patientDispatchEntity.CorrelationId) }
                    };

                    producer.Produce(nameof(KafkaTopic.DataAcquisitionRequested), new Message<string, DataAcquisitionRequestedValue>
                    {
                        Key = patientDispatchEntity.FacilityId,
                        Value = dataAcquisitionRequestedValue,
                        Headers = headers
                    });

                    producer.Flush();

                    _logger.LogInformation($"Produced Data Acquisition Requested event for facilityId: { patientDispatchEntity.FacilityId }");

                    _deletePatientDispatchCommand.Execute(patientDispatchEntity.FacilityId, patientDispatchEntity.PatientId);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to generate a Data Acquisition Requested event", ex);
                }
            }


            using (var producer = _auditProducerFactory.CreateAuditEventProducer())
            {
                var headers = new Headers
                {
                    { "X-Correlation-Id", Encoding.UTF8.GetBytes(patientDispatchEntity.CorrelationId) }
                };

                var auditMessage = new AuditEventMessage 
                { 
                    FacilityId = patientDispatchEntity.FacilityId,
                    ServiceName = QueryDispatchConstants.ServiceName,
                    Action = AuditEventType.Create,
                    EventDate = DateTime.UtcNow,
                    Resource = nameof(KafkaTopic.DataAcquisitionRequested),
                    Notes = $"Produced Data Acquisition Event for facilityId: {patientDispatchEntity.FacilityId}"                    
                };

                producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                {
                    Value = auditMessage,
                    Headers = headers
                });

                producer.Flush();
            }

            return Task.CompletedTask;
        }
    }
}
