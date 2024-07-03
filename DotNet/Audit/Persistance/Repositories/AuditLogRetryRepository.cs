using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;

namespace LantanaGroup.Link.Audit.Persistance.Repositories
{
    public class AuditLogRetryRepository(ILogger<EntityRepository<RetryEntity>> logger, AuditDbContext dbContext) : RetryRepositorySQL(logger, dbContext)
    {
    }
}
