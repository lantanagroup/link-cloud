using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Normalization.Domain.Repositories
{
    public class OperationSequenceRepository : EntityRepository<OperationSequence, NormalizationDbContext>
    {
        public OperationSequenceRepository(DbContext dbContext) : base(dbContext)
        {
        }
    }
}
