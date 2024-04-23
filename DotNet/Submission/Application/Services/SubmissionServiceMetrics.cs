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
            SubmissionCounter = meter.CreateCounter<long>("link_submission_service.submission.count");       
        }

        public Counter<long> SubmissionCounter { get; private set; }
        public void IncrementSubmissionCounter(List<KeyValuePair<string, object?>> tags)
        {
            SubmissionCounter.Add(1, tags.ToArray());
        }
    }
}
