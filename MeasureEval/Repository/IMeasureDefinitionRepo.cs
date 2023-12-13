using LantanaGroup.Link.MeasureEval.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

namespace LantanaGroup.Link.MeasureEval.Repository
{
    public interface IMeasureDefinitionRepo : IMongoDbRepository<MeasureDefinition>
    {
        public Task CreateAsync(MeasureDefinition entity, CancellationToken cancellationToken);

        public Task UpdateAsync(string measureDefinitionId, MeasureDefinition entity, CancellationToken cancellationToken);

        public Task<MeasureDefinition> GetAsync(string measureDefinitionId, CancellationToken cancellationToken);

        public Task<bool> HealthCheck();
    }
}