using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

public interface IMongoDbRepository<T> : IPersistenceRepository<T> where T : BaseEntity
{
}
