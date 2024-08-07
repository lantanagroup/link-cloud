using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.Census.Application.Repositories;

public class CensusEntityRepository<T> : EntityRepository<T> where T : BaseEntity
{
    public CensusEntityRepository(ILogger<CensusEntityRepository<T>> logger, CensusContext dbContext) : base(logger, dbContext)
    {

    }
}