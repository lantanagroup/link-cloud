using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;

namespace LantanaGroup.Link.DMRP.Data.Repository;

public class FacilityReportingPlanRepository : EntityRepository<FacilityReportingPlan, DmrpDbContext>
{
    public FacilityReportingPlanRepository(DmrpDbContext dbContext) : base(dbContext) { }
}
