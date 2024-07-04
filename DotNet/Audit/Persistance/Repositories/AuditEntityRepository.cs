using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.Audit.Persistance.Repositories
{
    public class AuditEntityRepository<T> : EntityRepository<T> where T : BaseEntity
    {
        public AuditEntityRepository(ILogger<AuditEntityRepository<T>> logger, AuditDbContext dbContext) : base(logger, dbContext)
        {

        }
    }
}
