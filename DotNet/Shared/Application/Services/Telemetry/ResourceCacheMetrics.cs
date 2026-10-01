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

            meter.CreateObservableGauge(
                DiagnosticNames.ResourceCacheQueueDepth,
                () => _queueDepth?.Invoke() ?? 0);
        }

        /// <inheritdoc/>
        public void RecordRead(string outcome, double milliseconds)
        {
            _readDuration.Record(milliseconds, new KeyValuePair<string, object?>(DiagnosticNames.CacheOutcome, outcome));
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
