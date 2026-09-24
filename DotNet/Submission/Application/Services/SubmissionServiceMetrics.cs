using System.Diagnostics.Metrics;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Submission.Application.Interfaces;
using LantanaGroup.Link.Submission.Settings;

namespace LantanaGroup.Link.Submission.Application.Services
{
    public class SubmissionServiceMetrics : ISubmissionServiceMetrics
    {
        public const string MeterName = $"Link.{SubmissionConstants.ServiceName}";

        private readonly Counter<long> _resourceCount;
        private readonly Counter<long> _uploadCount;
        private readonly Histogram<double> _uploadDuration;
        private readonly Histogram<long> _uploadSize;

        public SubmissionServiceMetrics(IMeterFactory meterFactory)
        {
            Meter meter = meterFactory.Create(MeterName);
            _resourceCount = meter.CreateCounter<long>("link_submission_resource_count");
            _uploadCount = meter.CreateCounter<long>(DiagnosticNames.SubmissionUploadCount);
            _uploadDuration = meter.CreateHistogram<double>("link_submission_upload_duration", "ms");
            _uploadSize = meter.CreateHistogram<long>("link_submission_upload_size", "By");
        }

        public List<KeyValuePair<string, object?>> BuildTags(string? correlationId, Guid reportScheduleId, string? patientId, string facilityId, string destinationType)
        {
            // correlationId / reportScheduleId / patientId are retained on the signature
            // for caller compatibility; they must not appear on Prometheus meters.
            _ = correlationId;
            _ = reportScheduleId;
            _ = patientId;
            return new()
            {
                new(DiagnosticNames.FacilityId, facilityId),
                new(DiagnosticNames.DestinationType, destinationType)
            };
        }

        public void IncrementResourceCount(List<KeyValuePair<string, object?>> tags)
        {
            _resourceCount.Add(1, tags.ToArray());
        }

        public void IncrementUploadCount(List<KeyValuePair<string, object?>> tags, string outcome)
        {
            var withOutcome = new List<KeyValuePair<string, object?>>(tags)
            {
                new(DiagnosticNames.Outcome, outcome)
            };
            _uploadCount.Add(1, withOutcome.ToArray());
        }

        public void RecordUploadDuration(double durationMilliseconds, List<KeyValuePair<string, object?>> tags)
        {
            _uploadDuration.Record(durationMilliseconds, tags.ToArray());
        }

        public void RecordUploadSize(long sizeBytes, List<KeyValuePair<string, object?>> tags)
        {
            _uploadSize.Record(sizeBytes, tags.ToArray());
        }
    }
}
