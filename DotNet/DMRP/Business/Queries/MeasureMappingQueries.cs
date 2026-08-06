using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;

namespace LantanaGroup.Link.DMRP.Business.Queries
{
    public interface IMeasureMappingQueries
    {
        Task<MeasureMappingModel?> GetAsync(string id, CancellationToken cancellationToken = default);

        Task<PagedMeasureMappingDto> PagedSearchAsync(string sortBy = "Id", SortOrder sortOrder = SortOrder.Descending,
            int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);
    }

    public class MeasureMappingQueries : IMeasureMappingQueries
    {
        private readonly IEntityRepository<MeasureMapping> _repository;

        public MeasureMappingQueries(IEntityRepository<MeasureMapping> repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<MeasureMappingModel?> GetAsync(string id, CancellationToken cancellationToken = default)
        {
            var entity = await _repository.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

            return entity == null ? null : ToModel(entity);
        }

        public async Task<PagedMeasureMappingDto> PagedSearchAsync(string sortBy = "Id",
            SortOrder sortOrder = SortOrder.Descending, int pageSize = 10, int pageNumber = 1,
            CancellationToken cancellationToken = default)
        {
            var (records, metadata) = await _repository.SearchAsync(m => true, sortBy, sortOrder,
                pageSize, pageNumber, cancellationToken);

            return new PagedMeasureMappingDto
            {
                Metadata = metadata,
                Records = records.Select(ToModel).ToList()
            };
        }

        private static MeasureMappingModel ToModel(MeasureMapping entity) => new() { Id = entity.Id };
    }
}
