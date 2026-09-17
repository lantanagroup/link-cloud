using System.Diagnostics.Metrics;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.DMRP.Scheduling
{
    public enum DmrpFireOutcome
    {
        Emitted,
        Skipped,
        Failed
    }

    /// <summary>
    /// How the nightly job's status is made visible: one counter per facility per fire, and one for
    /// every DMRP refresh that failed both attempts. The second one needs an alert - repeated
    /// failures mean facilities enter a month with last month's plans.
    /// </summary>
    public interface IDmrpSchedulingMetrics
    {
        void RecordFacilityOutcome(string timeZone, DmrpFireOutcome outcome);
        void RecordPlanRefreshFailure(string facilityId);
    }

    public sealed class DmrpSchedulingMetrics : IDmrpSchedulingMetrics
    {
        private readonly Counter<long> _outcomes;
        private readonly Counter<long> _refreshFailures;

        // The host's meter, so these export through the host's existing OpenTelemetry registration
        // (TelemetryServiceExtension adds exactly one meter, named Link.{ServiceName}).
        public DmrpSchedulingMetrics(IMeterFactory meterFactory, ServiceInformation serviceInformation)
        {
            ArgumentNullException.ThrowIfNull(meterFactory);
            ArgumentNullException.ThrowIfNull(serviceInformation);

            var meter = meterFactory.Create($"Link.{serviceInformation.ServiceName}");
            _outcomes = meter.CreateCounter<long>("link_dmrp.nightly_fire.facility_outcome.count");
            _refreshFailures = meter.CreateCounter<long>("link_dmrp.plan_refresh.failure.count");
        }

        public void RecordFacilityOutcome(string timeZone, DmrpFireOutcome outcome) =>
            _outcomes.Add(1,
                new KeyValuePair<string, object?>("time_zone", timeZone),
                new KeyValuePair<string, object?>("outcome", outcome.ToString().ToLowerInvariant()));

        public void RecordPlanRefreshFailure(string facilityId) =>
            _refreshFailures.Add(1, new KeyValuePair<string, object?>("facility_id", facilityId));
    }
}
