using LantanaGroup.Link.DMRP.Business.Mapping;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LinqKit;
using System.Linq.Expressions;

namespace LantanaGroup.Link.DMRP.Business.Queries
{
    public interface IFacilityReportingPlanQueries
    {
        Task<FacilityReportingPlanModel?> GetAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// All reporting plans for a facility, narrowed by any combination of period and reporting
        /// state. The same query serves callers asking about the current month (clock-derived
        /// arguments) and callers asking about a specific reporting period.
        /// </summary>
        /// <param name="facilityId">
        /// The reporting facility as the Tenant service knows it (the NHSN Org Id). A facility with no
        /// plans, and one that does not exist, both return an empty list - absence of enrollment is an
        /// answer, so the caller decides whether the facility itself is missing.
        /// </param>
        /// <param name="reportingMonth">Exact reporting month, 1-12. Null returns every month.</param>
        /// <param name="reportingYear">Exact reporting year. Null returns every year.</param>
        /// <param name="isReporting">
        /// True returns only the measures the facility is enrolled in, false only those it has
        /// withdrawn from. Null returns both: a withdrawal is recorded as false rather than deleted,
        /// so an unfiltered read is the facility's whole history.
        /// </param>
        /// <param name="periodRange">
        /// An inclusive window of reporting periods, for callers asking "what is coming up" rather
        /// than about one exact month. Combining it with <paramref name="reportingMonth"/> or
        /// <paramref name="reportingYear"/> narrows to the intersection; callers that treat the two
        /// as mutually exclusive refuse the combination before they get here.
        /// </param>
        /// <param name="cancellationToken">Cancels the read.</param>
        /// <returns>
        /// The matching plans, each resolved through its measure mapping so the measure, dQM and
        /// frequency are populated. A plan whose mapping could not be resolved is still returned, with
        /// those three left null.
        /// </returns>
        Task<List<FacilityReportingPlanModel>> GetForFacilityAsync(string facilityId, int? reportingMonth = null,
            int? reportingYear = null, bool? isReporting = null, ReportingPeriodRange? periodRange = null,
            CancellationToken cancellationToken = default);

        Task<PagedFacilityReportingPlanDto> PagedSearchAsync(string? facilityId = null, string? measureMappingId = null,
            int? reportingMonth = null, int? reportingYear = null, bool? isReporting = null, string sortBy = "Id",
            SortOrder sortOrder = SortOrder.Descending, int pageSize = 10, int pageNumber = 1,
            CancellationToken cancellationToken = default);
    }

    public class FacilityReportingPlanQueries : IFacilityReportingPlanQueries
    {
        private readonly IEntityRepository<FacilityReportingPlan> _repository;
        private readonly IEntityRepository<MeasureMapping> _measureMappings;

        public FacilityReportingPlanQueries(IEntityRepository<FacilityReportingPlan> repository,
            IEntityRepository<MeasureMapping> measureMappings)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _measureMappings = measureMappings ?? throw new ArgumentNullException(nameof(measureMappings));
        }

        public async Task<FacilityReportingPlanModel?> GetAsync(string id, CancellationToken cancellationToken = default)
        {
            var entity = await _repository.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

            return entity == null ? null : ToModel(entity);
        }

        public async Task<List<FacilityReportingPlanModel>> GetForFacilityAsync(string facilityId,
            int? reportingMonth = null, int? reportingYear = null, bool? isReporting = null,
            ReportingPeriodRange? periodRange = null, CancellationToken cancellationToken = default)
        {
            var entities = await _repository.FindAsync(
                MatchesFacilityPlan(facilityId, reportingMonth, reportingYear, isReporting, periodRange),
                cancellationToken);

            // The facility view labels rows by measure rather than mapping id, so resolve the
            // mappings and hang them on the navigation before mapping - the same two-query stitch
            // DbBackedReportingPlanSource uses (the shared repository has no Include).
            if (entities.Count > 0)
            {
                var mappingIds = entities.Select(p => p.MeasureMappingId).Distinct().ToList();
                var mappings = await _measureMappings.FindAsync(m => mappingIds.Contains(m.Id), cancellationToken);
                var mappingsById = mappings.ToDictionary(m => m.Id);

                foreach (var plan in entities)
                {
                    plan.MeasureMapping = mappingsById.GetValueOrDefault(plan.MeasureMappingId);
                }
            }

            return entities.Select(ToModel).ToList();
        }

        public async Task<PagedFacilityReportingPlanDto> PagedSearchAsync(string? facilityId = null,
            string? measureMappingId = null, int? reportingMonth = null, int? reportingYear = null,
            bool? isReporting = null, string sortBy = "Id", SortOrder sortOrder = SortOrder.Descending,
            int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default)
        {
            var (records, metadata) = await _repository.SearchAsync(p =>
                (facilityId == null || p.FacilityId == facilityId)
                && (measureMappingId == null || p.MeasureMappingId == measureMappingId)
                && (reportingMonth == null || p.ReportingMonth == reportingMonth)
                && (reportingYear == null || p.ReportingYear == reportingYear)
                && (isReporting == null || p.IsReporting == isReporting),
                sortBy, sortOrder, pageSize, pageNumber, cancellationToken);

            return new PagedFacilityReportingPlanDto
            {
                Metadata = metadata,
                Records = records.Select(ToModel).ToList()
            };
        }

        /// <summary>
        /// The filter behind the per-facility read: the facility, plus whichever of the optional
        /// narrowings the caller supplied. Each one is skipped when its argument is null, so the
        /// unfiltered call returns the facility's whole history.
        /// </summary>
        /// <remarks>
        /// Composed rather than written as one expression: a filter the caller left out contributes
        /// nothing at all, instead of a <c>value == null ||</c> guard that has to be read past here
        /// and evaluated in the database there. Same idiom the Report managers use.
        /// <para>
        /// The window is three clauses that read as one sentence: the period falls inside the span of
        /// years, and where it lands on the first or the last year of that span, it is not before the
        /// opening month or after the closing one. Splitting the year test from the month test is what
        /// makes it legible - "October 2026 through March 2027" is not a month range, because March
        /// is inside the window in one year and behind it in the other.
        /// </para>
        /// <para>
        /// Comparisons are on the year and month columns rather than arithmetic over them
        /// (<c>ReportingYear * 12 + ReportingMonth</c>). The arithmetic form is shorter and answers
        /// correctly, but no index can serve it, so it scans - and
        /// <c>IX_FacilityReportingPlans_Facility_Period</c> exists for exactly this read.
        /// </para>
        /// </remarks>
        private static Expression<Func<FacilityReportingPlan, bool>> MatchesFacilityPlan(string facilityId,
            int? reportingMonth, int? reportingYear, bool? isReporting, ReportingPeriodRange? window)
        {
            Expression<Func<FacilityReportingPlan, bool>> predicate = plan => plan.FacilityId == facilityId;

            if (reportingMonth.HasValue)
            {
                predicate = predicate.And(plan => plan.ReportingMonth == reportingMonth.Value);
            }

            if (reportingYear.HasValue)
            {
                predicate = predicate.And(plan => plan.ReportingYear == reportingYear.Value);
            }

            if (isReporting.HasValue)
            {
                predicate = predicate.And(plan => plan.IsReporting == isReporting.Value);
            }

            if (window.HasValue)
            {
                // Each bound reaches the expression as a plain local rather than as a walk through
                // window.Value.From.Year, which is a longer chain for the provider to fold away.
                var firstYear = window.Value.From.Year;
                var firstMonth = window.Value.From.Month;
                var lastYear = window.Value.To.Year;
                var lastMonth = window.Value.To.Month;

                // Inside the span of years...
                predicate = predicate.And(plan => plan.ReportingYear >= firstYear && plan.ReportingYear <= lastYear);

                // ...and within the opening and closing months of that span.
                predicate = predicate.And(plan => plan.ReportingYear != firstYear || plan.ReportingMonth >= firstMonth);
                predicate = predicate.And(plan => plan.ReportingYear != lastYear || plan.ReportingMonth <= lastMonth);
            }

            return predicate;
        }

        private static FacilityReportingPlanModel ToModel(FacilityReportingPlan entity) =>
            FacilityReportingPlanMapper.ToModel(entity);
    }
}
