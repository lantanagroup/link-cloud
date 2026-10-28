using LantanaGroup.Link.Shared.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;

namespace LantanaGroup.Link.Audit.Persistance.Repositories
{
    public class AuditEntityRepository<T> : BaseEntityRepository<T, AuditDbContext> where T : BaseEntity
    {
        public AuditEntityRepository(ILogger<AuditEntityRepository<T>> logger, AuditDbContext dbContext) : base(logger, dbContext)
        {

        }
    }
}
