using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Normalization.Domain.Repositories
{
    public class OperationRepository : EntityRepository<Operation, NormalizationDbContext>
    {
        public OperationRepository(DbContext dbContext) : base(dbContext)
        {
        }
    }
}
