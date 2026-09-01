using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.DMRP.Business
{
    /// <summary>
    /// A facility's reporting obligations as a calendar: one entry per reporting period, carrying the
    /// measures enrolled in it and the schedule Link will run for them.
    /// </summary>
    public interface IFacilityReportingPlanLookAhead
    {
        /// <summary>
        /// The facility's plan across a window of periods.
        /// </summary>
        /// <param name="facilityId">The reporting facility.</param>
        /// <param name="window">
        /// The periods to answer for. Null returns only the periods the facility has plans on record
        /// for, however far back or forward they run, and projects nothing - there is no bounded set
        /// of months to project into.
        /// </param>
        /// <param name="anchor">
        /// The period the facility is currently in. Its enrollment is what a period with no plan on
        /// record is projected from.
        /// </param>
        /// <param name="isReporting">
        /// Narrows the recorded rows. Projection runs only when this asks for current obligations -
        /// a withdrawal is a fact about a period that happened, not something to carry forward.
        /// </param>
        /// <param name="pageSize">Periods per page.</param>
        /// <param name="pageNumber">One-based page number.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        Task<PagedFacilityReportingPlanPeriodDto> GetAsync(string facilityId, ReportingPeriodRange? window,
            ReportingPeriod anchor, bool? isReporting = null, int pageSize = 10, int pageNumber = 1,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Answers from what DMRP has recorded, and projects the rest.
    /// </summary>
    /// <remarks>
    /// A reporting plan row says what a facility is enrolled to report in one period, and nothing
    /// writes rows for periods that have not happened yet. The measure's frequency, though, already
    /// says how often it reports - so the months in front of a facility can be derived from what it is
    /// enrolled in now rather than read from rows that do not exist.
    /// <para>
    /// Recorded always wins. A period with plans on record is reported as it stands, including a
    /// period whose enrollment differs from today's; only the gaps are projected. That way the answer
    /// improves on its own as a DMRP sync starts filling periods in, without this code changing.
    /// </para>
    /// </remarks>
    public sealed class FacilityReportingPlanLookAhead : IFacilityReportingPlanLookAhead
    {
        private readonly IFacilityReportingPlanQueries _queries;
        private readonly IReportingPlanSource _reportingPlans;
        private readonly IReportingPlanScheduleProjector _scheduleProjector;

        public FacilityReportingPlanLookAhead(IFacilityReportingPlanQueries queries,
            IReportingPlanSource reportingPlans,
            IReportingPlanScheduleProjector scheduleProjector)
        {
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
            _reportingPlans = reportingPlans ?? throw new ArgumentNullException(nameof(reportingPlans));
            _scheduleProjector = scheduleProjector ?? throw new ArgumentNullException(nameof(scheduleProjector));
        }

        public async Task<PagedFacilityReportingPlanPeriodDto> GetAsync(string facilityId,
            ReportingPeriodRange? window, ReportingPeriod anchor, bool? isReporting = null, int pageSize = 10,
            int pageNumber = 1, CancellationToken cancellationToken = default)
        {
            var recorded = await RecordedPeriodsAsync(facilityId, window, isReporting, cancellationToken);

            var periods = window is null
                ? recorded
                : await FillGapsAsync(facilityId, window.Value, anchor, isReporting, recorded, cancellationToken);

            var ordered = periods
                .OrderBy(period => period.ReportingYear)
                .ThenBy(period => period.ReportingMonth)
                .ToList();

            var page = ordered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            return new PagedFacilityReportingPlanPeriodDto(page,
                new PaginationMetadata(pageSize, pageNumber, ordered.Count));
        }

        /// <summary>
        /// The periods the facility has reporting plans on record for, each with the schedule those
        /// plans produce.
        /// </summary>
        private async Task<List<FacilityReportingPlanPeriodModel>> RecordedPeriodsAsync(string facilityId,
            ReportingPeriodRange? window, bool? isReporting, CancellationToken cancellationToken)
        {
            var plans = await _queries.GetForFacilityAsync(facilityId, isReporting: isReporting,
                periodRange: window, cancellationToken: cancellationToken);

            return plans
                .GroupBy(plan => new ReportingPeriod(plan.ReportingYear, plan.ReportingMonth))
                .Select(period => new FacilityReportingPlanPeriodModel
                {
                    ReportingYear = period.Key.Year,
                    ReportingMonth = period.Key.Month,

                    // Ordered by measure, then by mapping id to break the tie two mappings of the
                    // same measure would otherwise leave to the database's row order.
                    Measures = period
                        .OrderBy(plan => plan.Measure, StringComparer.Ordinal)
                        .ThenBy(plan => plan.MeasureMappingId, StringComparer.Ordinal)
                        .Select(ToMeasureModel)
                        .ToList(),

                    // Only enrollments the facility is actually reporting schedule anything. A
                    // withdrawn measure stays in Measures as history and contributes no report.
                    Schedule = _scheduleProjector.Project(
                        period.Where(plan => plan.IsReporting).Select(ToScheduleEntry).ToList(),
                        facilityId, period.Key),

                    IsProjected = false
                })
                .ToList();
        }

        /// <summary>
        /// Adds an entry for every period in the window the facility has no plan on record for,
        /// derived from what it is enrolled in now.
        /// </summary>
        private async Task<List<FacilityReportingPlanPeriodModel>> FillGapsAsync(string facilityId,
            ReportingPeriodRange window, ReportingPeriod anchor, bool? isReporting,
            List<FacilityReportingPlanPeriodModel> recorded, CancellationToken cancellationToken)
        {
            // A withdrawal is a fact about a period that already happened. Carrying one forward would
            // report a facility as not reporting in months nobody has said anything about yet.
            if (isReporting == false)
            {
                return recorded;
            }

            var gaps = window.Periods()
                .Where(period => !recorded.Any(r => r.ReportingYear == period.Year && r.ReportingMonth == period.Month))
                .ToList();

            if (gaps.Count == 0)
            {
                return recorded;
            }

            var current = await _reportingPlans.GetForPeriodAsync(facilityId, anchor.Month, anchor.Year,
                cancellationToken);

            if (current.Count == 0)
            {
                // Nothing to project from. The gaps stay absent rather than becoming empty periods,
                // which would read as "you report nothing that month" instead of "nobody has said".
                return recorded;
            }

            var measures = current
                .OrderBy(entry => entry.Measure, StringComparer.Ordinal)
                .Select(ToMeasureModel)
                .ToList();

            foreach (var period in gaps)
            {
                recorded.Add(new FacilityReportingPlanPeriodModel
                {
                    ReportingYear = period.Year,
                    ReportingMonth = period.Month,

                    // The same list every projected month over: the projection is the current
                    // enrollment, and the frequency on each measure is what says it recurs.
                    Measures = measures,
                    Schedule = _scheduleProjector.Project(current, facilityId, period),
                    IsProjected = true
                });
            }

            return recorded;
        }

        private static FacilityReportingPlanMeasureModel ToMeasureModel(FacilityReportingPlanModel plan) => new()
        {
            MeasureMappingId = plan.MeasureMappingId,
            Measure = plan.Measure,
            DQM = plan.DQM,
            Frequency = plan.Frequency,
            IsReporting = plan.IsReporting
        };

        /// <summary>
        /// A projected measure has no plan row behind it, so it carries no mapping id. It is an
        /// expectation about a period nobody has recorded yet, not a record of one.
        /// </summary>
        private static FacilityReportingPlanMeasureModel ToMeasureModel(ReportingPlanEntry entry) => new()
        {
            Measure = entry.Measure,
            DQM = entry.DQM,
            Frequency = entry.Frequency,
            IsReporting = true
        };

        private static ReportingPlanEntry ToScheduleEntry(FacilityReportingPlanModel plan) =>
            new(plan.Measure ?? string.Empty, plan.DQM ?? string.Empty, plan.Frequency ?? Frequency.Adhoc);
    }
}
