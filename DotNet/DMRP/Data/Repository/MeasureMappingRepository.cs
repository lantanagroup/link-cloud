using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;

namespace LantanaGroup.Link.DMRP.Data.Repository;

public class MeasureMappingRepository : EntityRepository<MeasureMapping, DmrpDbContext>
{
    public MeasureMappingRepository(DmrpDbContext dbContext) : base(dbContext) { }
}
