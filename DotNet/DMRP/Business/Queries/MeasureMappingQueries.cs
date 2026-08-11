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

        Task<PagedMeasureMappingDto> PagedSearchAsync(SearchMeasureMappingDto searchDto, CancellationToken cancellationToken = default);
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

        public async Task<PagedMeasureMappingDto> PagedSearchAsync(SearchMeasureMappingDto searchDto, CancellationToken cancellationToken = default)
        {
            var measure = string.IsNullOrWhiteSpace(searchDto.Measure) ? null : searchDto.Measure;
            var dqm = string.IsNullOrWhiteSpace(searchDto.DQM) ? null : searchDto.DQM;
            var frequency = searchDto.Frequency;

            var (records, metadata) = await _repository.SearchAsync(
                m => (measure == null || m.Measure == measure)
                    && (dqm == null || m.DQM == dqm)
                    && (!frequency.HasValue || m.Frequency == frequency.Value),
                searchDto.SortBy, searchDto.SortOrder,
                searchDto.PageSize, searchDto.PageNumber, cancellationToken);

            return new PagedMeasureMappingDto
            {
                Metadata = metadata,
                Records = records.Select(ToModel).ToList()
            };
        }

        private static MeasureMappingModel ToModel(MeasureMapping entity) => new()
        {
            Id = entity.Id,
            Measure = entity.Measure,
            DQM = entity.DQM,
            Frequency = entity.Frequency
        };
    }
}
