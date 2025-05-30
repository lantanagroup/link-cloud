using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;

namespace LantanaGroup.Link.Normalization.Domain.Repositories
{
    public class OperationRepository : EntityRepository<Operation, NormalizationDbContext>
    {
        public OperationRepository(NormalizationDbContext dbContext) : base(dbContext)
        {
        }
    }
}
