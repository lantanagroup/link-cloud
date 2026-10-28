using Confluent.Kafka;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using ReportingStatus = LantanaGroup.Link.Report.Domain.Enums.ReportingStatus;

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

        public async Task<bool> Produce(ReportScheduleModel schedule, List<string>? patientsToEvaluate = null, CancellationToken cancellationToken = default, string? metricsMode = null)
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

            // Delivery-report handler observes per-message delivery outcomes without blocking
            // the produce loop. Exceptions from ProduceAsync (one awaited round-trip per
            // message) used to be the only way to observe failures but also serialised the
            // loop; fire-and-forget Produce + a shared handler preserves error visibility
            // while letting librdkafka batch messages to the broker.
            var deliveryFailures = new ConcurrentBag<(string PatientId, Error Error)>();

            foreach (string patientId in patientsToEvaluate)
            {
                // IProducer<K,V>.Produce is fire-and-forget and has no CancellationToken
                // overload (only ProduceAsync does, which is what we're deliberately
                // avoiding for throughput). Honor cancellation cooperatively between
                // iterations so large batches still abort promptly on shutdown.
                cancellationToken.ThrowIfCancellationRequested();

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
                            ReportTrackingId = schedule.Id.ToString(),
                            StartDate = schedule.ReportStartDate.UtcDateTime,
                            EndDate = schedule.ReportEndDate.UtcDateTime,
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
                if (!string.IsNullOrWhiteSpace(metricsMode))
                {
                    KafkaHeaderHelper.SetMetricsMode(headers, metricsMode);
                }

                var capturedPatientId = patientId;
                _dataAcqProducer.Produce(nameof(KafkaTopic.DataAcquisitionRequested),
                    new Message<string, DataAcquisitionRequestedValue>
                    {
                        Key = darKey,
                        Value = darValue,
                        Headers = headers
                    },
                    deliveryReport =>
                    {
                        if (deliveryReport.Error.IsError)
                        {
                            deliveryFailures.Add((capturedPatientId, deliveryReport.Error));
                        }
                    });
            }

            // Flush waits for all outstanding delivery reports. Using the caller's
            // CancellationToken ensures we never block longer than the surrounding consume
            // pipeline allows (e.g., on consumer shutdown or Kafka poll timeout) and lets
            // the broker acknowledge the whole batch in parallel rather than one patient at
            // a time.
            _dataAcqProducer.Flush(cancellationToken);

            if (!deliveryFailures.IsEmpty)
            {
                // ConcurrentBag is unordered, so any element we pull is a sample — not
                // the chronologically first failure. Surface a bounded sample of patient
                // IDs/reasons to aid diagnostics without blowing up the exception message.
                var sample = deliveryFailures.Take(5).ToList();
                var sampleText = string.Join("; ", sample.Select(f => $"patient {f.PatientId} - {f.Error.Reason}"));

                throw new ProduceException<string, DataAcquisitionRequestedValue>(
                    sample[0].Error,
                    new DeliveryResult<string, DataAcquisitionRequestedValue>
                    {
                        Topic = nameof(KafkaTopic.DataAcquisitionRequested)
                    },
                    new Exception(
                        $"{deliveryFailures.Count} DataAcquisitionRequested delivery failure(s) for schedule {schedule.Id}. " +
                        $"Sample failure(s) (up to {sample.Count}): {sampleText}"));
            }

            return true;
        }
    }
}
