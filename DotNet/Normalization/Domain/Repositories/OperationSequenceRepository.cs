using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;

namespace LantanaGroup.Link.Normalization.Domain.Repositories
{
    public class OperationSequenceRepository : EntityRepository<OperationSequence, NormalizationDbContext>
    {
        public OperationSequenceRepository(NormalizationDbContext dbContext) : base(dbContext)
        {
        }
    }
}
