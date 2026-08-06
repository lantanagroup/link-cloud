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

        Task<PagedFacilityReportingPlanDto> PagedSearchAsync(string sortBy = "Id", SortOrder sortOrder = SortOrder.Descending,
            int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);
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

        public async Task<PagedFacilityReportingPlanDto> PagedSearchAsync(string sortBy = "Id",
            SortOrder sortOrder = SortOrder.Descending, int pageSize = 10, int pageNumber = 1,
            CancellationToken cancellationToken = default)
        {
            var (records, metadata) = await _repository.SearchAsync(p => true, sortBy, sortOrder,
                pageSize, pageNumber, cancellationToken);

            return new PagedFacilityReportingPlanDto
            {
                Metadata = metadata,
                Records = records.Select(ToModel).ToList()
            };
        }

        private static FacilityReportingPlanModel ToModel(FacilityReportingPlan entity) => new() { Id = entity.Id };
    }
}
