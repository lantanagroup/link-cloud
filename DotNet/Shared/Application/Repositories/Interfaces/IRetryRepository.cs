using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
public interface IRetryRepository : IPersistenceRepository<RetryEntity>
{
    Task<List<RetryEntity>> GetAllAsync(CancellationToken cancellationToken = default);
}
