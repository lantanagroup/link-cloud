using LantanaGroup.Link.DataAcquisition.Domain;
using LantanaGroup.Link.Shared.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Domain
{
    public class DataEntityRepository<T> : BaseEntityRepository<T> where T : BaseEntity
    {
        //This is important so that the Data Acquisition Entity Repos have an instance of DataAcquisitionDbContext instead of the base DbContext
        public DataEntityRepository(ILogger<BaseEntityRepository<T>> logger, DataAcquisitionDbContext dbContext) : base(logger, dbContext)
        {

        }
    }
}
