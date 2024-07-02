using LantanaGroup.Link.DataAcquisition.Domain;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories;

public class RetryRepository_SQL_DataAcq : RetryRepositorySQL
{
    public RetryRepository_SQL_DataAcq(ILogger<EntityRepository<RetryEntity>> logger, DataAcquisitionDbContext dbContext) : base(logger, dbContext)
    {
    }
}
