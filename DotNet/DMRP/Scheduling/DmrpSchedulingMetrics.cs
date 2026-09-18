using System.Diagnostics.Metrics;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;

namespace LantanaGroup.Link.DMRP.Scheduling
{
    public enum DmrpFireOutcome
    {
        Emitted,
        Skipped,
        Failed
    }

    /// <summary>
    /// How the nightly job's status is made visible: one counter per produced event, one per facility
    /// per fire, and one for every DMRP refresh that failed both attempts. The last needs an alert -
    /// repeated failures mean facilities enter a month with last month's plans.
    /// </summary>
    public interface IDmrpSchedulingMetrics
    {
        /// <summary>
        /// One per ReportScheduled produced, the counterpart of the classic job's
        /// link_tenant_service.report_scheduled.count, so the flag can be turned on without losing
        /// sight of how many reports are being scheduled.
        /// </summary>
        /// <remarks>
        /// <paramref name="frequency"/> is part of the call so the whole of what was announced is at
        /// this seam, but it is deliberately not a tag: the point of the counter is to line up
        /// one-for-one with the classic one during the rollout, and a dimension the classic counter
        /// does not carry would stop the two being compared directly. The frequency of a fire is in
        /// the job's log line either way.
        /// </remarks>
        void RecordReportScheduled(string facilityId, string frequency, string[] reportTypes, DateTime startUtc,
            DateTime endUtc);

        void RecordFacilityOutcome(string timeZone, DmrpFireOutcome outcome);
        void RecordPlanRefreshFailure(string facilityId);
    }

    public sealed class DmrpSchedulingMetrics : IDmrpSchedulingMetrics
    {
        private readonly Counter<long> _reportsScheduled;
        private readonly Counter<long> _outcomes;
        private readonly Counter<long> _refreshFailures;

        // The host's meter, so these export through the host's existing OpenTelemetry registration.
        // TelemetryServiceExtension subscribes to exactly one meter, Link.{name}, where name is the
        // constant the host passes to AddLinkTelemetry ("Tenant"). SetupServiceInformation stores that
        // same constant in ServiceConfigName; ServiceName is the display name from configuration
        // ("Link Tenant Service"), and a meter built from it is never exported.
        public DmrpSchedulingMetrics(IMeterFactory meterFactory, ServiceInformation serviceInformation)
        {
            ArgumentNullException.ThrowIfNull(meterFactory);
            ArgumentNullException.ThrowIfNull(serviceInformation);
            if (string.IsNullOrWhiteSpace(serviceInformation.ServiceConfigName))
            {
                throw new ArgumentException(
                    $"{nameof(ServiceInformation)}.{nameof(ServiceInformation.ServiceConfigName)} is required; " +
                    "it names the meter the host exports.", nameof(serviceInformation));
            }

            var meter = meterFactory.Create($"Link.{serviceInformation.ServiceConfigName}");
            _reportsScheduled = meter.CreateCounter<long>("link_dmrp.report_scheduled.count");
            _outcomes = meter.CreateCounter<long>("link_dmrp.nightly_fire.facility_outcome.count");
            _refreshFailures = meter.CreateCounter<long>("link_dmrp.plan_refresh.failure.count");
        }

        // The same tag names and values TenantServiceMetrics records against the classic counter,
        // including report.type carrying the whole dQM array, so a dashboard built on one reads the
        // other without a second shape to learn.
        public void RecordReportScheduled(string facilityId, string frequency, string[] reportTypes,
            DateTime startUtc, DateTime endUtc) =>
            _reportsScheduled.Add(1,
                new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId),
                new KeyValuePair<string, object?>(DiagnosticNames.ReportType, reportTypes),
                new KeyValuePair<string, object?>(DiagnosticNames.PeriodStart, startUtc),
                new KeyValuePair<string, object?>(DiagnosticNames.PeriodEnd, endUtc));

        public void RecordFacilityOutcome(string timeZone, DmrpFireOutcome outcome) =>
            _outcomes.Add(1,
                new KeyValuePair<string, object?>("time_zone", timeZone),
                new KeyValuePair<string, object?>("outcome", outcome.ToString().ToLowerInvariant()));

        public void RecordPlanRefreshFailure(string facilityId) =>
            _refreshFailures.Add(1, new KeyValuePair<string, object?>("facility_id", facilityId));
    }
}
