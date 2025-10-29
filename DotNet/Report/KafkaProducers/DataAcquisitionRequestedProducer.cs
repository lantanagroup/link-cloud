using Confluent.Kafka;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using System.Diagnostics;
using System.Text;
using LantanaGroup.Link.Report.Services;

namespace LantanaGroup.Link.Report.KafkaProducers
{
    public class DataAcquisitionRequestedProducer
    {
        private readonly IDatabase _database;
        private readonly IProducer<string, DataAcquisitionRequestedValue> _dataAcqProducer;

        private static readonly ActivitySource _fallbackActivitySource = new ActivitySource("FallbackSource");

        public DataAcquisitionRequestedProducer(IDatabase database, IProducer<string, DataAcquisitionRequestedValue> dataAcqProducer) 
        {
            _database = database;
            _dataAcqProducer = dataAcqProducer;
        }

        public async Task<bool> Produce(ReportScheduleModel schedule, List<string>? patientsToEvaluate = null)
        {
            if (patientsToEvaluate == null || patientsToEvaluate.Count == 0)
            {
                patientsToEvaluate = (await _database.SubmissionEntryRepository.FindAsync(x => x.ReportScheduleId == schedule.Id && x.Status == PatientSubmissionStatus.PendingEvaluation)).Select(x => x.PatientId).Distinct().ToList();
            }

            string reportableEvent = string.Empty;

            switch (schedule.Frequency)
            {
                case Frequency.Monthly:
                    reportableEvent = "EOM";
                    break;
                case Frequency.Weekly:
                    reportableEvent = "EOW";
                    break;
                case Frequency.Daily:
                    reportableEvent = "EOD";
                    break;
                case Frequency.Adhoc:
                    reportableEvent = "Adhoc";
                    break;
            }

            foreach (string patientId in patientsToEvaluate)
            {
                // Generate the trace and span ID first
                string traceId = ActivityTraceId.CreateRandom().ToHexString();
                string spanId = ActivitySpanId.CreateRandom().ToHexString();
                // Create a traceparent W3C format: version-traceId-spanId-flags
                string traceparentValue = $"00-{traceId}-{spanId}-01";
                // Create activity context from the generated IDs
                var activityContext = new ActivityContext(
                    ActivityTraceId.CreateFromString(traceId.AsSpan()),
                    ActivitySpanId.CreateFromString(spanId.AsSpan()),
                    ActivityTraceFlags.Recorded);

                ActivitySource activitySource = ServiceActivitySource.Instance ?? _fallbackActivitySource;

                
                using var activity = activitySource.StartActivity(
                    "ProduceDataAcquisitionRequested", 
                    ActivityKind.Producer,
                    activityContext);
                activity?.SetTag("patientId", patientId);
                activity?.SetTag("facilityId", schedule.FacilityId);
                activity?.SetTag("reportScheduleId", schedule.Id);

                var darKey = schedule.FacilityId;
                var darValue = new DataAcquisitionRequestedValue()
                {
                    PatientId = patientId,
                    ReportableEvent = reportableEvent,
                    ScheduledReports = new List<ScheduledReport>()
                    {
                        new ()
                        {
                            ReportTrackingId = schedule.Id!,
                            StartDate = schedule.ReportStartDate,
                            EndDate = schedule.ReportEndDate,
                            Frequency = schedule.Frequency,
                            ReportTypes = schedule.ReportTypes
                        }
                    },
                    QueryType = QueryType.Initial.ToString(),
                };

                var headers = new Headers
                {
                    { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) }
                };
                headers.Add("traceparent", Encoding.UTF8.GetBytes(traceparentValue));
                
                _dataAcqProducer.Produce(nameof(KafkaTopic.DataAcquisitionRequested), 
                    new Message<string, DataAcquisitionRequestedValue> 
                    { 
                        Key = darKey, 
                        Value = darValue, 
                        Headers = headers 
                    });

                _dataAcqProducer.Flush();
            }

            return true;
        }
    }
}
