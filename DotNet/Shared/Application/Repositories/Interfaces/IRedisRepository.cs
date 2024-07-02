using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

public interface IRedisRepository<T> : IEntityRepository<T> where T : BaseEntity
{
}
