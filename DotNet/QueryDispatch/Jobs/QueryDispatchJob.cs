using Confluent.Kafka;
using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Interfaces;
using Quartz;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using System.Text;
using QueryDispatch.Application.Settings;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using QueryDispatch.Domain.Managers;
using QueryDispatch.Application.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LanatanGroup.Link.QueryDispatch.Jobs
{
    [DisallowConcurrentExecution]
    public class QueryDispatchJob : IJob
    {
        private readonly ILogger<QueryDispatchJob> _logger;
        private readonly IProducer<string, DataAcquisitionRequestedValue> _acquisitionProducer;
        private readonly IProducer<string, AuditEventMessage> _auditProducer;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public QueryDispatchJob(
            ILogger<QueryDispatchJob> logger,
            IServiceScopeFactory serviceScopeFactory,
            IProducer<string, DataAcquisitionRequestedValue> acquisitionProducer,
            IProducer<string, AuditEventMessage> auditProducer)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _acquisitionProducer = acquisitionProducer;
            _auditProducer = auditProducer;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            JobDataMap triggerMap = context.Trigger.JobDataMap!;
            PatientDispatchEntity patientDispatchEntity = (PatientDispatchEntity)triggerMap["PatientDispatchEntity"];

            ProducerConfig config = new ProducerConfig()
            {
                ClientId = "QueryDispatch_DataAcquisitionScheduled"
            };


            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var patientDispatchMgr = scope.ServiceProvider.GetRequiredService<IPatientDispatchManager>();

                DataAcquisitionRequestedValue dataAcquisitionRequestedValue = new DataAcquisitionRequestedValue()
                {
                    PatientId = patientDispatchEntity.PatientId,
                    ScheduledReports = new List<ScheduledReport>(),
                    QueryType = QueryTypes.Initial.ToString(),
                    ReportableEvent = ReportableEvents.Discharge.ToString()
                };

                foreach (var scheduledReportPeriod in patientDispatchEntity.ScheduledReportPeriods)
                {
                    dataAcquisitionRequestedValue.ScheduledReports.Add(new ScheduledReport
                    {
                        ReportTypes = scheduledReportPeriod.ReportTypes,
                        Frequency = scheduledReportPeriod.Frequency,
                        StartDate = scheduledReportPeriod.StartDate,
                        EndDate = scheduledReportPeriod.EndDate
                    });
                }

                var acqHeaders = new Headers
                    {
                        { "X-Correlation-Id", Encoding.UTF8.GetBytes(patientDispatchEntity.CorrelationId) }
                    };

                _acquisitionProducer.Produce(nameof(KafkaTopic.DataAcquisitionRequested), new Message<string, DataAcquisitionRequestedValue>
                {
                    Key = patientDispatchEntity.FacilityId,
                    Value = dataAcquisitionRequestedValue,
                    Headers = acqHeaders
                });

                _acquisitionProducer.Flush();

                _logger.LogInformation($"Produced Data Acquisition Requested event for facilityId: {HtmlInputSanitizer.Sanitize(patientDispatchEntity.FacilityId)}");

                await patientDispatchMgr.deletePatientDispatch(patientDispatchEntity.FacilityId, patientDispatchEntity.PatientId);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate a Data Acquisition Requested event");
            }




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

            _auditProducer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
            {
                Value = auditMessage,
                Headers = headers
            });

            _auditProducer.Flush();

        }
    }
}
