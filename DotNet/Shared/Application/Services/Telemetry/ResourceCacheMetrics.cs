using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Shared.Application.Services.Telemetry
{
    /// <inheritdoc cref="IResourceCacheMetrics"/>
    public class ResourceCacheMetrics : IResourceCacheMetrics
    {
        private readonly Histogram<double> _readDuration;
        private readonly Histogram<double> _writeDuration;
        private readonly Histogram<double> _queueWaitDuration;
        private readonly Histogram<double> _drainWaitDuration;
        private readonly Counter<long> _writeRetryCounter;
        private readonly Counter<long> _durableCountReadFailureCounter;

        private Func<int>? _queueDepth;

        public ResourceCacheMetrics(IMeterFactory meterFactory, ServiceInformation serviceInformation)
        {
            ArgumentNullException.ThrowIfNull(meterFactory);
            ArgumentNullException.ThrowIfNull(serviceInformation);

            // Must match the meter OpenTelemetry registers, or none of this is exported.
            var meter = meterFactory.Create($"Link.{serviceInformation.ServiceConfigName}");

            _readDuration = meter.CreateHistogram<double>(DiagnosticNames.ResourceCacheReadDuration, "ms");
            _writeDuration = meter.CreateHistogram<double>(DiagnosticNames.ResourceCacheWriteDuration, "ms");
            _queueWaitDuration = meter.CreateHistogram<double>(DiagnosticNames.ResourceCacheQueueWaitDuration, "ms");
            _drainWaitDuration = meter.CreateHistogram<double>(DiagnosticNames.ResourceCacheDrainWaitDuration, "ms");
            _writeRetryCounter = meter.CreateCounter<long>(DiagnosticNames.ResourceCacheWriteRetryCount);
            _durableCountReadFailureCounter =
                meter.CreateCounter<long>(DiagnosticNames.ResourceCacheDurableCountReadFailureCount);

            meter.CreateObservableGauge(
                DiagnosticNames.ResourceCacheQueueDepth,
                () => _queueDepth?.Invoke() ?? 0);
        }

        /// <inheritdoc/>
        public void RecordRead(string outcome, string? fallbackReason, double milliseconds)
        {
            // Omitted rather than recorded with a null value. Not for Prometheus' sake -- there a
            // selector of "" matches an absent label too -- but because OTLP carries the attribute
            // set as given, so an empty value is a real dimension downstream and makes this a
            // different metric stream from the one MeasureEval emits for the same condition.
            if (fallbackReason is null)
            {
                _readDuration.Record(
                    milliseconds,
                    new KeyValuePair<string, object?>(DiagnosticNames.CacheOutcome, outcome));
                return;
            }

            _readDuration.Record(
                milliseconds,
                new KeyValuePair<string, object?>(DiagnosticNames.CacheOutcome, outcome),
                new KeyValuePair<string, object?>(DiagnosticNames.CacheFallbackReason, fallbackReason));
        }

        /// <inheritdoc/>
        public void IncrementDurableCountReadFailure()
        {
            _durableCountReadFailureCounter.Add(1);
        }

        /// <inheritdoc/>
        public void RecordWrite(string store, string outcome, double milliseconds)
        {
            _writeDuration.Record(
                milliseconds,
                new KeyValuePair<string, object?>(DiagnosticNames.CacheStore, store),
                new KeyValuePair<string, object?>(DiagnosticNames.CacheOutcome, outcome));
        }

        /// <inheritdoc/>
        public void RecordQueueWait(double milliseconds)
        {
            _queueWaitDuration.Record(milliseconds);
        }

        /// <inheritdoc/>
        public void RecordDrainWait(double milliseconds)
        {
            _drainWaitDuration.Record(milliseconds);
        }

        /// <inheritdoc/>
        public void IncrementWriteRetry(string outcome)
        {
            _writeRetryCounter.Add(1, new KeyValuePair<string, object?>(DiagnosticNames.CacheOutcome, outcome));
        }

        /// <inheritdoc/>
        public void TrackQueueDepth(Func<int> queueDepth)
        {
            _queueDepth = queueDepth;
        }
    }
}
