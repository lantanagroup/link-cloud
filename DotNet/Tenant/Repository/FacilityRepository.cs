using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Repository.Context;
namespace LantanaGroup.Link.Tenant.Repository;

public class FacilityRepository : EntityRepository<Facility, TenantDbContext>
{
    public FacilityRepository(TenantDbContext dbContext) : base(dbContext) { }
}
