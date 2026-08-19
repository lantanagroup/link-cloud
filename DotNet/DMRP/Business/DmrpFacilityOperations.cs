using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.DMRP.Business
{
    /// <summary>
    /// Adds NHSN measure enrollment to the host's facility operations. A facility's scheduled reports
    /// stop being something a caller supplies and become something derived from the facility's DMRP
    /// reporting plans, and a facility that is removed outright takes its reporting plans with it.
    /// </summary>
    /// <remarks>
    /// Every operation still runs the host's (tenant service) implementation. This type owns only the difference
    /// DMRP makes, so the host remains the single place facilities are validated, persisted and
    /// scheduled.
    /// </remarks>
    public sealed class DmrpFacilityOperations : IFacilityOperations
    {
        private readonly ILogger<DmrpFacilityOperations> _logger;
        private readonly IFacilityOperations _hostImplementation;
        private readonly IReportingPlanSource _reportingPlans;
        private readonly IFacilityReportingPlanManager _reportingPlanManager;
        private readonly TimeProvider _timeProvider;

        public DmrpFacilityOperations(ILogger<DmrpFacilityOperations> logger,
            IFacilityOperations hostImplementation,
            IReportingPlanSource reportingPlans,
            IFacilityReportingPlanManager reportingPlanManager,
            TimeProvider timeProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hostImplementation = hostImplementation ?? throw new ArgumentNullException(nameof(hostImplementation));
            _reportingPlans = reportingPlans ?? throw new ArgumentNullException(nameof(reportingPlans));
            _reportingPlanManager = reportingPlanManager ?? throw new ArgumentNullException(nameof(reportingPlanManager));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        public async Task CreateAsync(FacilityModel facility, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(facility);

            RejectCallerSuppliedSchedule(facility);

            facility.ScheduledReports = await BuildScheduleAsync(facility, cancellationToken);

            await _hostImplementation.CreateAsync(facility, cancellationToken);
        }

        public async Task UpdateAsync(FacilityModel existingFacility, FacilityModel updatedFacility,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(existingFacility);
            ArgumentNullException.ThrowIfNull(updatedFacility);

            RejectCallerSuppliedSchedule(updatedFacility);

            // The period is read from the timezone being saved, not the one on record, so a facility
            // that moves timezone in the same request is scheduled against the period it moved to.
            updatedFacility.ScheduledReports = await BuildScheduleAsync(updatedFacility, cancellationToken);

            await _hostImplementation.UpdateAsync(existingFacility, updatedFacility, cancellationToken);
        }

        /// <summary>
        /// Removes the facility and then its reporting plans. The order matters: the host's delete can
        /// still refuse, and plans deleted ahead of a refused delete would leave a facility that
        /// reports nothing.
        /// </summary>
        public async Task DeleteAsync(string facilityId, CancellationToken cancellationToken = default)
        {
            await _hostImplementation.DeleteAsync(facilityId, cancellationToken);

            var removed = await _reportingPlanManager.DeleteForFacilityAsync(facilityId, cancellationToken);

            _logger.LogInformation("Deleted {Count} reporting plan(s) belonging to removed facility {FacilityId}",
                removed, facilityId.SanitizeForLog());
        }

        /// <summary>
        /// Soft delete keeps the facility's reporting plans. The facility can be restored, and the
        /// plans are the record of what DMRP said it was enrolled to report while it was active.
        /// </summary>
        public Task SoftDeleteAsync(string facilityId, CancellationToken cancellationToken = default) =>
            _hostImplementation.SoftDeleteAsync(facilityId, cancellationToken);

        public Task RestoreAsync(FacilityModel facility, CancellationToken cancellationToken = default) =>
            _hostImplementation.RestoreAsync(facility, cancellationToken);

        /// <summary>
        /// Turns the facility's enrolled measures into the schedule the host stores, grouping the dQMs
        /// by the frequency their measure mapping carries.
        /// </summary>
        private async Task<TenantScheduledReportConfig> BuildScheduleAsync(FacilityModel facility,
            CancellationToken cancellationToken)
        {
            var facilityId = facility.FacilityId;

            if (string.IsNullOrWhiteSpace(facilityId))
            {
                // The host rejects this on its own with a message naming the field. Returning an empty
                // schedule lets that happen instead of failing here on a lookup that cannot succeed.
                return EmptySchedule();
            }

            var (month, year) = CurrentPeriod(facility);

            var entries = await _reportingPlans.GetForPeriodAsync(facilityId, month, year, cancellationToken);

            if (entries.Count == 0)
            {
                _logger.LogInformation(
                    "Facility {FacilityId} has no reporting plans for {Month}/{Year}; it is scheduled for no reports.",
                    facilityId.SanitizeForLog(), month, year);

                return EmptySchedule();
            }

            var unmapped = entries.Where(e => string.IsNullOrWhiteSpace(e.DQM)).ToList();

            foreach (var entry in unmapped)
            {
                // The scheduling workflow records a measure DMRP returned that Link has no mapping for
                // with a null dQM, precisely so it shows up here rather than being lost.
                _logger.LogWarning(
                    "Facility {FacilityId} is enrolled in measure {Measure} for {Month}/{Year}, which has no dQM mapped. It is excluded from the facility's schedule.",
                    facilityId.SanitizeForLog(), entry.Measure.SanitizeForLog(), month, year);
            }

            return new TenantScheduledReportConfig
            {
                Daily = DqmsFor(entries, Frequency.Daily),
                Weekly = DqmsFor(entries, Frequency.Weekly),
                Monthly = DqmsFor(entries, Frequency.Monthly)
            };
        }

        /// <summary>
        /// The distinct dQMs reported at one frequency. Two NHSN measures can map to the same dQM — the
        /// ADR's example is a patient safety measure and a medication safety measure both under ACH
        /// Monthly — and the host refuses a schedule that names one twice.
        /// </summary>
        private static string[] DqmsFor(IReadOnlyList<ReportingPlanEntry> entries, Frequency frequency) =>
            entries.Where(e => e.Frequency == frequency && !string.IsNullOrWhiteSpace(e.DQM))
                .Select(e => e.DQM)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

        /// <summary>
        /// The reporting period the facility is currently in, read in its own timezone so a facility
        /// near a month boundary is scheduled against the month it is actually in.
        /// </summary>
        private (int Month, int Year) CurrentPeriod(FacilityModel facility)
        {
            var utcNow = _timeProvider.GetUtcNow();

            if (string.IsNullOrWhiteSpace(facility.TimeZone))
            {
                return (utcNow.Month, utcNow.Year);
            }

            try
            {
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(facility.TimeZone);
                var localNow = TimeZoneInfo.ConvertTime(utcNow, timeZone);

                return (localNow.Month, localNow.Year);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // The host validates the timezone and answers with a message naming it. Falling back
                // to UTC here lets the request reach that validation rather than failing first with an
                // error about reporting periods.
                _logger.LogWarning(ex,
                    "Facility {FacilityId} has an unusable timezone; the reporting period was read in UTC instead.",
                    facility.FacilityId?.SanitizeForLog());

                return (utcNow.Month, utcNow.Year);
            }
        }

        /// <summary>
        /// The remedy the message names has to be one the caller can actually carry out. Leaving
        /// scheduledReports out of the request body is not: its three arrays are non-nullable, so model
        /// binding rejects an absent block before this ever runs. An empty block is what gets through.
        /// </summary>
        private static void RejectCallerSuppliedSchedule(FacilityModel facility)
        {
            if (!HasScheduledReports(facility.ScheduledReports))
            {
                return;
            }

            throw new ScheduledReportsNotAcceptedException(
                "Scheduled reports cannot be set on a facility while DMRP is enabled. They are derived from the facility's DMRP reporting plans. Resubmit with empty daily, weekly and monthly arrays in scheduledReports.");
        }

        private static bool HasScheduledReports(TenantScheduledReportConfig? schedule) =>
            schedule is not null
            && ((schedule.Daily?.Length ?? 0) > 0
                || (schedule.Weekly?.Length ?? 0) > 0
                || (schedule.Monthly?.Length ?? 0) > 0);

        private static TenantScheduledReportConfig EmptySchedule() => new()
        {
            Daily = Array.Empty<string>(),
            Weekly = Array.Empty<string>(),
            Monthly = Array.Empty<string>()
        };
    }
}
