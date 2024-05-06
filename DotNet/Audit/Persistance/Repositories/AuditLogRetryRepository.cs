using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;

namespace LantanaGroup.Link.Audit.Persistance.Repositories
{
    public class AuditLogRetryRepository(ILogger<BaseSqlConfigurationRepo<RetryEntity>> logger, AuditDbContext dbContext) : RetryRepository_SQL(logger, dbContext)
    {
    }
}
