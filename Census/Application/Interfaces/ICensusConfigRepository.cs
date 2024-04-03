using Census.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

namespace LantanaGroup.Link.Census.Application.Interfaces;

public interface ICensusConfigRepository : ISqlPersistenceRepository<CensusConfigEntity>
{
}
