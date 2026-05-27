using System.Diagnostics.Metrics;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services.Telemetry;

namespace LantanaGroup.Link.Normalization.Application.Services
{
    public interface INormalizationServiceMetrics
    {
        void IncrementResourceChangedCounter(List<KeyValuePair<string, object?>> tags, bool changed);
        TrackedRequestDuration MeasureNormalizationDuration(List<KeyValuePair<string, object?>> tags);
    }

    public class NormalizationServiceMetrics : INormalizationServiceMetrics
    {
        private readonly TimeProvider _timeProvider;
        private readonly Histogram<double> _normalizationDuration;
        private readonly Counter<long> _resourceChangedCounter;
        private readonly Counter<long> _resourceNotChangedCounter;

        public NormalizationServiceMetrics(IMeterFactory meterFactory, TimeProvider timeProvider, ServiceInformation serviceInformation)
        {
            _timeProvider = timeProvider;
            
            // Use the configured service name for the meter name
            Meter meter = meterFactory.Create($"Link.{serviceInformation.ServiceConfigName}");
            _normalizationDuration = meter.CreateHistogram<double>(DiagnosticNames.NormalizationDuration, "ms");
            _resourceChangedCounter = meter.CreateCounter<long>(DiagnosticNames.NormalizationResourceChangedCount);
            _resourceNotChangedCounter = meter.CreateCounter<long>(DiagnosticNames.NormalizationResourceNotChangedCount);
        }
        
        public void IncrementResourceChangedCounter(List<KeyValuePair<string, object?>> tags, bool changed)
        {
            if (changed)
                _resourceChangedCounter.Add(1, tags.ToArray());
            else
                _resourceNotChangedCounter.Add(1, tags.ToArray());
        }

        public TrackedRequestDuration MeasureNormalizationDuration(List<KeyValuePair<string, object?>> tags)
        {
            return new TrackedRequestDuration(_normalizationDuration, _timeProvider, tags);
        }
    }
}
