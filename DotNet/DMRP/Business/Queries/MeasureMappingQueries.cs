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
            // Trim: a copy-pasted or hastily typed " cms1 " should match CMS130v13, not miss on
            // padding. ToLowerInvariant: the parameter is lowered in C#, where the current culture
            // could otherwise diverge from the SQL LOWER() applied to the column (e.g. Turkish
            // dotless i).
            var measure = string.IsNullOrWhiteSpace(searchDto.Measure) ? null : searchDto.Measure.Trim().ToLowerInvariant();
            var dqm = string.IsNullOrWhiteSpace(searchDto.DQM) ? null : searchDto.DQM.Trim().ToLowerInvariant();
            var frequency = searchDto.Frequency;

            // Substring match: the Admin UI filters as the admin types, so every partial value has
            // to narrow the list rather than answer empty until the full value matches. Lowering
            // both sides makes the promised case-insensitivity explicit instead of an accident of
            // the database collation (and holds under the tests' SQLite provider too).
            var (records, metadata) = await _repository.SearchAsync(
                m => (measure == null || m.Measure.ToLower().Contains(measure))
                    && (dqm == null || m.DQM.ToLower().Contains(dqm))
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
