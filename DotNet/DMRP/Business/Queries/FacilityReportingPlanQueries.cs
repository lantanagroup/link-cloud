using LantanaGroup.Link.DMRP.Business.Mapping;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;

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
        Task<List<FacilityReportingPlanModel>> GetForFacilityAsync(string facilityId, int? reportingMonth = null,
            int? reportingYear = null, bool? isReporting = null, CancellationToken cancellationToken = default);

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
            CancellationToken cancellationToken = default)
        {
            var entities = await _repository.FindAsync(p => p.FacilityId == facilityId
                && (reportingMonth == null || p.ReportingMonth == reportingMonth)
                && (reportingYear == null || p.ReportingYear == reportingYear)
                && (isReporting == null || p.IsReporting == isReporting), cancellationToken);

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

        private static FacilityReportingPlanModel ToModel(FacilityReportingPlan entity) =>
            FacilityReportingPlanMapper.ToModel(entity);
    }
}
