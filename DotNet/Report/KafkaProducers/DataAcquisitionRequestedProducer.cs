using Confluent.Kafka;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using System.Diagnostics;
using System.Text;

namespace LantanaGroup.Link.Report.KafkaProducers
{
    public class DataAcquisitionRequestedProducer
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IProducer<string, DataAcquisitionRequestedValue> _dataAcqProducer;

        private static readonly ActivitySource _fallbackActivitySource = new ActivitySource("FallbackSource");

        public DataAcquisitionRequestedProducer(IServiceScopeFactory serviceScopeFactory, IProducer<string, DataAcquisitionRequestedValue> dataAcqProducer)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _dataAcqProducer = dataAcqProducer;
        }

        public async Task<bool> Produce(ReportScheduleModel schedule, List<string>? patientsToEvaluate = null)
        {
            var _database = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<IDatabase>();

            //TODO: Ensure that ReportEntry statuses are updated for each entry
            if (patientsToEvaluate == null || patientsToEvaluate.Count == 0)
            {
                patientsToEvaluate = (await _database.ReportEntryRepository.FindAsync(x => x.ReportScheduleId == schedule.Id && x.ReportingStatus == ReportingStatus.PatientIdentified)).Select(x => x.PatientId).Distinct().ToList();
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

                var reportStartDateUtc = schedule.ReportStartDate.Kind == DateTimeKind.Utc
                    ? schedule.ReportStartDate
                    : DateTime.SpecifyKind(schedule.ReportStartDate, DateTimeKind.Utc);

                var reportEndDateUtc = schedule.ReportEndDate.Kind == DateTimeKind.Utc
                    ? schedule.ReportEndDate
                    : DateTime.SpecifyKind(schedule.ReportEndDate, DateTimeKind.Utc);

                var darKey = schedule.FacilityId;
                var darValue = new DataAcquisitionRequestedValue()
                {
                    PatientId = patientId,
                    ReportableEvent = reportableEvent,
                    ScheduledReports = new List<ScheduledReport>()
                    {
                        new ()
                        {
                            ReportTrackingId = schedule.Id.ToString(),
                            StartDate = reportStartDateUtc,
                            EndDate = reportEndDateUtc,
                            Frequency = schedule.Frequency,
                            ReportTypes = schedule.ReportTypes,
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
