using LantanaGroup.Link.Normalization.Domain.Entities;

namespace LantanaGroup.Link.Normalization.Domain.Repositories
{
    public class OperationRepository : Shared.Domain.Repositories.Implementations.EntityRepository<Operation, NormalizationDbContext>
    {
        public OperationRepository(NormalizationDbContext dbContext) : base(dbContext)
        {
        }
    }
}
