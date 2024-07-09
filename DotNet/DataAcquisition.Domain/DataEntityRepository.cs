using LantanaGroup.Link.DataAcquisition.Domain;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Domain
{
    public class DataEntityRepository<T> : EntityRepository<T> where T : BaseEntity
    {
        //This is important so that the Data Acquisition Entity Repos have an instance of DataAcquisitionDbContext instead of the base DbContext
        public DataEntityRepository(ILogger<EntityRepository<T>> logger, DataAcquisitionDbContext dbContext) : base(logger, dbContext)
        {

        }
    }
}
