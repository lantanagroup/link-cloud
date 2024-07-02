using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Census.Application.Repositories;

public class RetryRepositorySQLCensus : RetryRepositorySQL
{
    public RetryRepositorySQLCensus(ILogger<BaseSqlConfigurationRepo<RetryEntity>> logger, CensusContext dbContext) : base(logger, dbContext)
    {
    }
}
