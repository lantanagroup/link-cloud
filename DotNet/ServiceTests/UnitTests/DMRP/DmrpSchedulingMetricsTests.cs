using System.Diagnostics.Metrics;
using LantanaGroup.Link.DMRP.Scheduling;
using LantanaGroup.Link.Shared.Application.Models;

namespace UnitTests.DMRP
{
    /// <summary>
    /// The host's OpenTelemetry registration subscribes to exactly one meter, Link.{ServiceConfigName}
    /// (the constant the host passes to AddLinkTelemetry, e.g. "Tenant"), not the display name held in
    /// ServiceInformation.ServiceName ("Link Tenant Service"). A counter on any other meter increments
    /// in memory and is never exported, so the meter name is the thing this pins.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class DmrpSchedulingMetricsTests
    {
        private static readonly ServiceInformation TenantLike = new()
        {
            ServiceName = "Link Tenant Service",
            ServiceConfigName = "Tenant"
        };

        [Fact]
        public void Counters_are_created_on_the_hosts_exported_meter()
        {
            var factory = new RecordingMeterFactory();
            var seen = new List<(string Meter, string Instrument, long Value)>();
            using var listener = new MeterListener
            {
                InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == "Link.Tenant") l.EnableMeasurementEvents(instrument);
                }
            };
            listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
                seen.Add((instrument.Meter.Name, instrument.Name, value)));
            listener.Start();

            var metrics = new DmrpSchedulingMetrics(factory, TenantLike);
            metrics.RecordReportScheduled("fac", "Daily", ["dqm"], DateTime.UtcNow, DateTime.UtcNow);
            metrics.RecordFacilityOutcome("US/Eastern", DmrpFireOutcome.Emitted);
            metrics.RecordPlanRefreshFailure("fac");

            Assert.Equal(["Link.Tenant"], factory.Created.Select(m => m.Name).Distinct());
            Assert.Equal(
                ["link_dmrp.report_scheduled.count", "link_dmrp.nightly_fire.facility_outcome.count", "link_dmrp.plan_refresh.failure.count"],
                seen.Select(s => s.Instrument));
            Assert.All(seen, s => Assert.Equal(1, s.Value));
        }

        [Fact]
        public void Refuses_service_information_without_a_config_name()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                new DmrpSchedulingMetrics(new RecordingMeterFactory(), new ServiceInformation { ServiceName = "Link Tenant Service" }));

            Assert.Contains(nameof(ServiceInformation.ServiceConfigName), ex.Message);
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
