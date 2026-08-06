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

        public FacilityReportingPlanQueries(IEntityRepository<FacilityReportingPlan> repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
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

        private static FacilityReportingPlanModel ToModel(FacilityReportingPlan entity) => new()
        {
            Id = entity.Id,
            FacilityId = entity.FacilityId,
            MeasureMappingId = entity.MeasureMappingId,
            ReportingMonth = entity.ReportingMonth,
            ReportingYear = entity.ReportingYear,
            IsReporting = entity.IsReporting,
            CreateDate = entity.CreateDate,
            ModifyDate = entity.ModifyDate
        };
    }
}
