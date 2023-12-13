using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

public interface IRedisRepository<T> : IPersistenceRepository<T> where T : BaseEntity
{
}
