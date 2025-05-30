using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Shared.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;

namespace LantanaGroup.Link.Census.Application.Repositories;

public class CensusEntityRepository<T> : BaseEntityRepository<T> where T : BaseEntity
{
    public CensusEntityRepository(ILogger<CensusEntityRepository<T>> logger, CensusContext dbContext) : base(logger, dbContext)
    {

    }
}