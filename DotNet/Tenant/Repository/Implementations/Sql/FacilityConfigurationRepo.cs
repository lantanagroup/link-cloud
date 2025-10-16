using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Repository.Context;
using LantanaGroup.Link.Tenant.Repository.Interfaces.Sql;
namespace LantanaGroup.Link.Tenant.Repository.Implementations.Sql;

public class FacilityConfigurationRepo : BaseEntityRepository<FacilityConfigModel, FacilityDbContext>, IFacilityConfigurationRepo
{
    public FacilityConfigurationRepo(ILogger<FacilityConfigurationRepo> logger, FacilityDbContext dbContext) : base(logger, dbContext){} 
}
