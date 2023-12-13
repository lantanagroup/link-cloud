using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

namespace LantanaGroup.Link.Normalization.Application.Services
{
    public interface IConfigRepository : IMongoDbRepository<NormalizationConfigEntity>
    {
        Task<bool> HealthCheck();
    }
}
