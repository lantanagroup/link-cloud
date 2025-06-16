using LantanaGroup.Link.Normalization.Application.Settings;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Normalization.Application.Services
{
    public interface INormalizationServiceMetrics
    {
        void IncrementResourceNormalizedCounter(List<KeyValuePair<string, object?>> tags);
    }

    public class NormalizationServiceMetrics : INormalizationServiceMetrics
    {
        public const string MeterName = $"Link.{NormalizationConstants.ServiceName}";

        public NormalizationServiceMetrics(IMeterFactory meterFactory)
        {
            Meter meter = meterFactory.Create(MeterName);
            ResourceNormalizedCounter = meter.CreateCounter<long>("link_normalization_service.resource_normalized.count");
        }

        public Counter<long> ResourceNormalizedCounter { get; private set; }
        public void IncrementResourceNormalizedCounter(List<KeyValuePair<string, object?>> tags)
        {
            ResourceNormalizedCounter.Add(1, tags.ToArray());
        }
    }
}
