using LantanaGroup.Link.DataAcquisition.Domain;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories;

public class RetryRepositorySQLDataAcq : RetryRepositorySQL
{
    public RetryRepositorySQLDataAcq(ILogger<EntityRepository<RetryEntity>> logger, DataAcquisitionDbContext dbContext) : base(logger, dbContext)
    {
    }
}
