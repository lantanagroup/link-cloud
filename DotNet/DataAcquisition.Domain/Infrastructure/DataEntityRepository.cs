using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure
{
    public class DataEntityRepository<T, TDbContext> : EntityRepository<T, TDbContext> where T : BaseEntity where TDbContext : DbContext
    {
        //This is important so that the Data Acquisition Entity Repos have an instance of DataAcquisitionDbContext instead of the base DbContext
        public DataEntityRepository(DataAcquisitionDbContext dbContext) : base(dbContext)
        {
        }
    }

    public class DataRetryEntityRepository : BaseEntityRepository<RetryEntity> 
    {
        //This is important so that the Data Acquisition Entity Repos have an instance of DataAcquisitionDbContext instead of the base DbContext
        public DataRetryEntityRepository(ILogger<DataRetryEntityRepository> logger, DataAcquisitionDbContext dbContext) : base(logger, dbContext)
        {
        }
    }
}
