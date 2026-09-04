using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;

namespace LantanaGroup.Link.DMRP.Business
{
    /// <summary>
    /// Reads a facility's reporting plan out of the module's own tables, resolving each row through
    /// its measure mapping.
    /// </summary>
    /// <remarks>
    /// This is the source of reporting plans until the DMRP API client exists. It reports what Link
    /// last recorded rather than what the API says right now, so a plan is only as current as the last
    /// write to the reporting plans table.
    /// </remarks>
    public sealed class DbBackedReportingPlanSource : IReportingPlanSource
    {
        private readonly ILogger<DbBackedReportingPlanSource> _logger;
        private readonly IEntityRepository<FacilityReportingPlan> _plans;
        private readonly IEntityRepository<MeasureMapping> _measureMappings;

        public DbBackedReportingPlanSource(ILogger<DbBackedReportingPlanSource> logger,
            IEntityRepository<FacilityReportingPlan> plans,
            IEntityRepository<MeasureMapping> measureMappings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _plans = plans ?? throw new ArgumentNullException(nameof(plans));
            _measureMappings = measureMappings ?? throw new ArgumentNullException(nameof(measureMappings));
        }

        public async Task<IReadOnlyList<ReportingPlanEntry>> GetForPeriodAsync(string facilityId, int month, int year,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(facilityId);

            var plans = await _plans.FindAsync(p => p.FacilityId == facilityId
                && p.ReportingMonth == month
                && p.ReportingYear == year
                && p.IsReporting, cancellationToken);

            if (plans.Count == 0)
            {
                return Array.Empty<ReportingPlanEntry>();
            }

            var mappingIds = plans
                .Select(p => p.MeasureMappingId)
                .Where(id => id is not null)
                .Select(id => id!)
                .Distinct()
                .ToList();

            var mappings = await _measureMappings.FindAsync(m => mappingIds.Contains(m.Id), cancellationToken);

            var mappingsById = mappings.ToDictionary(m => m.Id);

            var entries = new List<ReportingPlanEntry>(plans.Count);

            foreach (var plan in plans)
            {
                if (plan.MeasureMappingId is null)
                {
                    // An enrollment recorded before anyone mapped its measure. It is reported with no
                    // dQM rather than dropped -- the schedule projector excludes it and says so, which
                    // is the whole reason the row is storable. Dropping it here would lose the fact
                    // that the facility is enrolled in something Link cannot yet evaluate.
                    entries.Add(new ReportingPlanEntry(plan.Measure, string.Empty, null));
                    continue;
                }

                if (!mappingsById.TryGetValue(plan.MeasureMappingId, out var mapping))
                {
                    // A mapped plan whose mapping is gone: the foreign key means this is a read that
                    // raced a delete rather than an ordinary miss.
                    _logger.LogWarning(
                        "Reporting plan {PlanId} for facility {FacilityId} references measure mapping {MeasureMappingId}, which was not found. The measure is excluded from the facility's schedule.",
                        plan.Id.SanitizeForLog(), facilityId.SanitizeForLog(), plan.MeasureMappingId.SanitizeForLog());

                    continue;
                }

                entries.Add(new ReportingPlanEntry(mapping.Measure, mapping.DQM, mapping.Frequency));
            }

            return entries;
        }
    }
}
