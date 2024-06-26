﻿using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Census.Application.Repositories;

public class RetryRepositorySQLCensus : RetryRepository_SQL
{
    public RetryRepositorySQLCensus(ILogger<EntityRepository<RetryEntity>> logger, CensusContext dbContext) : base(logger, dbContext)
    {
    }
}
