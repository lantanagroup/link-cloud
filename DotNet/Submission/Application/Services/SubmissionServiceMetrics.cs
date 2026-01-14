using LantanaGroup.Link.Submission.Application.Interfaces;
using LantanaGroup.Link.Submission.Settings;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Submission.Application.Services
{
    public class SubmissionServiceMetrics : ISubmissionServiceMetrics
    {
        public const string MeterName = $"Link.{SubmissionConstants.ServiceName}";   

        public SubmissionServiceMetrics(IMeterFactory meterFactory)
        {         
            Meter meter = meterFactory.Create(MeterName);
            ResourcesSubmittedCounter = meter.CreateCounter<long>("link_submission_service.resources_submitted.count");
            ResourceTypeCounter = meter.CreateCounter<long>("link_submission_service.resource_type_submitted.count");
            MedicationCodeCounter = meter.CreateCounter<long>("link_submission_service.medication_code_submitted.count");
            ReportSubmittedCounter = meter.CreateCounter<long>("link_submission_service.reports_submitted.count");
            EncounterCounter = meter.CreateCounter<long>("link_submission_service.encounters_submitted.count");
            LocationCounter = meter.CreateCounter<long>("link_submission_service.locations_submitted.count");
            DiagnosticsCounter = meter.CreateCounter<long>("link_submission_service.diagnostics_submitted.count");
            MedicationRequestCounter = meter.CreateCounter<long>("link_submission_service.medication_requests_submitted.count");
            ObservationCounter = meter.CreateCounter<long>("link_submission_service.observations_submitted.count");
            SpecimenCounter = meter.CreateCounter<long>("link_submission_service.specimens_submitted.count");
            ServiceRequestCounter = meter.CreateCounter<long>("link_submission_service.service_requests_submitted.count");
        }

        public Counter<long> DiagnosticsCounter { get; private set; }
        public void IncrementDiagnosticCounter(int count, List<KeyValuePair<string, object?>> tags)
        {
            DiagnosticsCounter.Add(count, tags.ToArray());
        }

        public Counter<long> LocationCounter { get; private set; }
        public void IncrementLocationCounter(int count, List<KeyValuePair<string, object?>> tags)
        {
            LocationCounter.Add(count, tags.ToArray());
        }

        public Counter<long> EncounterCounter { get; private set; }
        public void IncrementEncounterCounter(int count, List<KeyValuePair<string, object?>> tags)
        {
            EncounterCounter.Add(count, tags.ToArray());
        }

        public Counter<long> ReportSubmittedCounter { get; private set; }
        public void IncrementReportSubmittedCounter(int count, List<KeyValuePair<string, object?>> tags)
        {
            ReportSubmittedCounter.Add(count, tags.ToArray());
        }

        public Counter<long> ResourcesSubmittedCounter { get; private set; }
        public void IncrementResourcesSubmittedCounter(int count, List<KeyValuePair<string, object?>> tags)
        {
            ResourcesSubmittedCounter.Add(count, tags.ToArray());
        }

        public Counter<long> ResourceTypeCounter { get; private set; }
        public void IncrementResourceTypeCounter(int count, List<KeyValuePair<string, object?>> tags)
        {
            ResourceTypeCounter.Add(count, tags.ToArray());
        }

        public Counter<long> MedicationCodeCounter { get; private set; }
        public void IncrementMedicationCounter(int count, List<KeyValuePair<string, object?>> tags)
        {
            MedicationCodeCounter.Add(count, tags.ToArray());
        }

        public Counter<long> MedicationRequestCounter{ get; private set; }
        public void IncrementMedicationRequestCounter(int count, List<KeyValuePair<string, object?>> tags)
        {
            MedicationRequestCounter.Add(count, tags.ToArray());
        }

        public Counter<long> ObservationCounter { get; private set; }
        public void IncrementObservationCounter(int count, List<KeyValuePair<string, object?>> tags)
        {
            ObservationCounter.Add(count, tags.ToArray());
        }

        public Counter<long> SpecimenCounter { get; private set; }
        public void IncrementSpecimenCounter(int count, List<KeyValuePair<string, object?>> tags)
        {
            SpecimenCounter.Add(count, tags.ToArray());
        }

        public Counter<long> ServiceRequestCounter { get; private set; }
        public void IncrementServiceRequestCounter(int count, List<KeyValuePair<string, object?>> tags)
        {
            ServiceRequestCounter.Add(count, tags.ToArray());
        }
    }
}
