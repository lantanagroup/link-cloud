using Confluent.Kafka;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Quartz;
using QueryDispatch.Application.Interfaces;
using QueryDispatch.Application.Models;
using QueryDispatch.Application.Settings;
using QueryDispatch.Domain.Managers;
using System.Diagnostics;
using System.Text;

namespace LanatanGroup.Link.QueryDispatch.Jobs
{
    [DisallowConcurrentExecution]
    public class QueryDispatchJob : IJob
    {
        private readonly ILogger<QueryDispatchJob> _logger;
        private readonly IProducer<string, DataAcquisitionRequestedValue> _acquisitionProducer;
        private readonly IProducer<string, AuditEventMessage> _auditProducer;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IQueryDispatchServiceMetrics _metrics;

        public QueryDispatchJob(
            ILogger<QueryDispatchJob> logger,
            IServiceScopeFactory serviceScopeFactory,
            IProducer<string, DataAcquisitionRequestedValue> acquisitionProducer,
            IProducer<string, AuditEventMessage> auditProducer,
            IQueryDispatchServiceMetrics metrics)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _acquisitionProducer = acquisitionProducer;
            _auditProducer = auditProducer;
            _metrics = metrics;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            JobDataMap triggerMap = context.Trigger.JobDataMap!;
            var patientDispatchEntity = triggerMap.GetObject<PatientDispatchEntity>("PatientDispatchEntity");

            ProducerConfig config = new ProducerConfig()
            {
                ClientId = "QueryDispatch_DataAcquisitionScheduled"
            };


            var stopwatch = Stopwatch.StartNew();
            var outcome = "success";
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var patientDispatchMgr = scope.ServiceProvider.GetRequiredService<IPatientDispatchManager>();

                var dataAcquisitionRequestedValue = new DataAcquisitionRequestedValue()
                {
                    PatientId = patientDispatchEntity.PatientId,
                    ScheduledReports = new List<ScheduledReport>(),
                    QueryType = QueryType.Initial.ToString(),
                    ReportableEvent = ReportableEvents.Discharge.ToString()
                };

                foreach (var scheduledReportPeriod in patientDispatchEntity.ScheduledReportPeriods)
                {
                    dataAcquisitionRequestedValue.ScheduledReports.Add(new ScheduledReport
                    {
                        ReportTypes = scheduledReportPeriod.ReportTypes,
                        Frequency = (Frequency)scheduledReportPeriod.Frequency,
                        StartDate = scheduledReportPeriod.StartDate,
                        EndDate = scheduledReportPeriod.EndDate,
                        ReportTrackingId = scheduledReportPeriod.ReportTrackingId
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

                _logger.LogInformation("Produced Data Acquisition Requested event for facilityId: {FacilityId}", HtmlInputSanitizer.Sanitize(patientDispatchEntity.FacilityId));

                await patientDispatchMgr.deletePatientDispatch(patientDispatchEntity.FacilityId, patientDispatchEntity.PatientId);

            }
            catch (Exception ex)
            {
                outcome = "failure";
                _logger.LogError(ex, "Failed to generate a Data Acquisition Requested event");
            }
            finally
            {
                stopwatch.Stop();
                if (patientDispatchEntity != null)
                {
                    _metrics.RecordDispatchDuration(patientDispatchEntity.FacilityId, stopwatch.Elapsed.TotalMilliseconds);
                    _metrics.IncrementPatientsDispatched(patientDispatchEntity.FacilityId, outcome);
                }
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
