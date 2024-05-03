using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Normalization.Application.Services;

public class RetryRepository_SQL_Norm : RetryRepository_SQL
{
    public RetryRepository_SQL_Norm(ILogger<BaseSqlConfigurationRepo<RetryEntity>> logger, NormalizationDbContext dbContext) : base(logger, dbContext)
    {
    }
}
