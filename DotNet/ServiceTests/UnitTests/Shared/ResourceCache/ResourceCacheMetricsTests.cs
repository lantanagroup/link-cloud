using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services.Telemetry;
using System.Diagnostics.Metrics;

namespace UnitTests.Shared.ResourceCache
{
    /// <summary>
    /// Pins the exported shape of the read instrument, which MeasureEval also writes to. The tag
    /// vocabulary is only comparable across the two runtimes if the same conditions produce the same
    /// labels, so these assert the exported measurement rather than the call into the interface.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class ResourceCacheMetricsTests
    {
        private static readonly ServiceInformation NormalizationLike = new()
        {
            ServiceName = "Link Normalization Service",
            ServiceConfigName = "Normalization"
        };

        [Fact]
        public void RecordRead_without_a_reason_omits_the_tag_rather_than_recording_it_empty()
        {
            using var recorder = new Recorder();

            new ResourceCacheMetrics(recorder.Factory, NormalizationLike)
                .RecordRead(ResourceCacheOutcomes.Hit, fallbackReason: null, 12.5);

            var read = Assert.Single(recorder.Measurements);
            Assert.Equal(DiagnosticNames.ResourceCacheReadDuration, read.Instrument);
            Assert.Equal(ResourceCacheOutcomes.Hit, read.Tags[DiagnosticNames.CacheOutcome]);

            // Not "present and empty". An explicit null exports as a label with an empty value, which
            // is a different Prometheus series from the absent label MeasureEval produces on a hit, so
            // one query would silently miss the other.
            Assert.DoesNotContain(DiagnosticNames.CacheFallbackReason, read.Tags.Keys);
        }

        [Theory]
        [InlineData(ResourceCacheOutcomes.Fallback, ResourceCacheFallbackReasons.Miss)]
        [InlineData(ResourceCacheOutcomes.Fallback, ResourceCacheFallbackReasons.Partial)]
        [InlineData(ResourceCacheOutcomes.Fallback, ResourceCacheFallbackReasons.Unavailable)]
        [InlineData(ResourceCacheOutcomes.Empty, ResourceCacheFallbackReasons.Unavailable)]
        public void RecordRead_with_a_reason_carries_both_tags(string outcome, string reason)
        {
            using var recorder = new Recorder();

            new ResourceCacheMetrics(recorder.Factory, NormalizationLike).RecordRead(outcome, reason, 12.5);

            var read = Assert.Single(recorder.Measurements);
            Assert.Equal(outcome, read.Tags[DiagnosticNames.CacheOutcome]);
            Assert.Equal(reason, read.Tags[DiagnosticNames.CacheFallbackReason]);
        }

        [Fact]
        public void The_durable_count_failure_counter_is_created_on_the_hosts_exported_meter()
        {
            using var recorder = new Recorder();

            new ResourceCacheMetrics(recorder.Factory, NormalizationLike).IncrementDurableCountReadFailure();

            // A counter on any other meter increments in memory and is never exported.
            Assert.Equal(["Link.Normalization"], recorder.Factory.Created.Select(m => m.Name).Distinct());

            var failure = Assert.Single(recorder.Measurements);
            Assert.Equal(DiagnosticNames.ResourceCacheDurableCountReadFailureCount, failure.Instrument);
            Assert.Equal(1d, failure.Value);
        }

        private sealed record Measurement(string Instrument, double Value, Dictionary<string, string?> Tags);

        /// <summary>
        /// Listens to the meter the host subscribes to and flattens every measurement, whatever its
        /// value type, so the tags can be asserted by key.
        /// </summary>
        private sealed class Recorder : IDisposable
        {
            private readonly MeterListener _listener;

            public Recorder()
            {
                _listener = new MeterListener
                {
                    InstrumentPublished = (instrument, l) =>
                    {
                        if (instrument.Meter.Name == "Link.Normalization")
                        {
                            l.EnableMeasurementEvents(instrument);
                        }
                    }
                };

                _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
                _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
                _listener.Start();
            }

            public RecordingMeterFactory Factory { get; } = new();

            public List<Measurement> Measurements { get; } = [];

            public void Dispose()
            {
                _listener.Dispose();
                Factory.Dispose();
            }

            private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
            {
                var flattened = new Dictionary<string, string?>();

                foreach (var tag in tags)
                {
                    flattened[tag.Key] = tag.Value?.ToString();
                }

                Measurements.Add(new Measurement(instrument.Name, value, flattened));
            }
        }

        private sealed class RecordingMeterFactory : IMeterFactory
        {
            public List<Meter> Created { get; } = [];

            public Meter Create(MeterOptions options)
            {
                var meter = new Meter(options);
                Created.Add(meter);
                return meter;
            }

            public void Dispose() => Created.ForEach(m => m.Dispose());
        }
    }
}
