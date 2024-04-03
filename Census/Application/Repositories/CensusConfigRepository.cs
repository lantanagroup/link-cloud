using Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Census.Application.Repositories;

public class CensusConfigRepository : BaseSqlConfigurationRepo<CensusConfigEntity>, ICensusConfigRepository
{
    public CensusConfigRepository(ILogger<BaseSqlConfigurationRepo<CensusConfigEntity>> logger, CensusContext dbContext) : base(logger, dbContext)
    {
    }
}
