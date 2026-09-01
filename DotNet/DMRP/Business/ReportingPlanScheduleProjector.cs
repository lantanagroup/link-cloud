using LantanaGroup.Link.DMRP.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.DMRP.Business
{
    /// <summary>
    /// Turns a facility's enrolled measures into the schedule Link runs for them, grouping the dQMs by
    /// the frequency their measure mapping carries.
    /// </summary>
    /// <remarks>
    /// Shared on purpose. The schedule stored on a facility and the schedule shown in a facility's
    /// look-ahead have to be derived the same way, or the facility is told it will report something
    /// Link is not going to run. Anything that needs to answer "what does this enrollment schedule"
    /// calls this rather than reimplementing the rules.
    /// </remarks>
    public interface IReportingPlanScheduleProjector
    {
        /// <summary>
        /// The schedule a set of enrollments produces.
        /// </summary>
        /// <param name="entries">The facility's enrollments for one period.</param>
        /// <param name="facilityId">Only for logging, so a dropped measure can be traced.</param>
        /// <param name="period">Only for logging.</param>
        TenantScheduledReportConfig Project(IReadOnlyList<ReportingPlanEntry> entries, string facilityId,
            ReportingPeriod period);
    }

    public sealed class ReportingPlanScheduleProjector : IReportingPlanScheduleProjector
    {
        private readonly ILogger<ReportingPlanScheduleProjector> _logger;

        public ReportingPlanScheduleProjector(ILogger<ReportingPlanScheduleProjector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public TenantScheduledReportConfig Project(IReadOnlyList<ReportingPlanEntry> entries, string facilityId,
            ReportingPeriod period)
        {
            ArgumentNullException.ThrowIfNull(entries);

            foreach (var entry in entries.Where(e => string.IsNullOrWhiteSpace(e.DQM)))
            {
                // A measure DMRP returned that Link has no mapping for is recorded with a null dQM
                // precisely so it shows up here rather than being lost.
                _logger.LogWarning(
                    "Facility {FacilityId} is enrolled in measure {Measure} for {Month}/{Year}, which has no dQM mapped. It is excluded from the facility's schedule.",
                    facilityId.SanitizeForLog(), entry.Measure.SanitizeForLog(), period.Month, period.Year);
            }

            // Only these three produce a recurring schedule. Discharge and Adhoc enrollments are not
            // dropped by accident - Link has no timer to hang them on, so naming them here would
            // promise a report nothing is going to run.
            return new TenantScheduledReportConfig
            {
                Daily = DqmsFor(entries, Frequency.Daily),
                Weekly = DqmsFor(entries, Frequency.Weekly),
                Monthly = DqmsFor(entries, Frequency.Monthly)
            };
        }

        /// <summary>
        /// The distinct dQMs reported at one frequency. Two NHSN measures can map to the same dQM - the
        /// ADR's example is a patient safety measure and a medication safety measure both under ACH
        /// Monthly - and the host refuses a schedule that names one twice.
        /// </summary>
        private static string[] DqmsFor(IReadOnlyList<ReportingPlanEntry> entries, Frequency frequency) =>
            entries.Where(e => e.Frequency == frequency && !string.IsNullOrWhiteSpace(e.DQM))
                .Select(e => e.DQM)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

        /// <summary>A schedule that runs nothing.</summary>
        public static TenantScheduledReportConfig EmptySchedule() => new()
        {
            Daily = Array.Empty<string>(),
            Weekly = Array.Empty<string>(),
            Monthly = Array.Empty<string>()
        };
    }
}
