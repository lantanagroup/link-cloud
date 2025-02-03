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
        }

        public Counter<long> ResourcesSubmittedCounter { get; private set; }
        public void IncrementResourcesSubmittedCounter(int resourcesSubmitted, List<KeyValuePair<string, object?>> tags)
        {
            ResourcesSubmittedCounter.Add(resourcesSubmitted, tags.ToArray());
        }

        public Counter<long> ResourceTypeCounter { get; private set; }
        public void IncrementResourceTypeCounter(int resourceTypeCount, List<KeyValuePair<string, object?>> tags)
        {
            ResourceTypeCounter.Add(resourceTypeCount, tags.ToArray());
        }

        public Counter<long> MedicationCodeCounter { get; private set; }
        public void IncrementMedicationCounter(int medicationCount, List<KeyValuePair<string, object?>> tags)
        {
            MedicationCodeCounter.Add(medicationCount, tags.ToArray());
        }
    }
}
