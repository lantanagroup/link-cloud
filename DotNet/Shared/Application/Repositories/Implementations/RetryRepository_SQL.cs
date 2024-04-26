using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.Shared.Application.Repositories.Implementations;
public class RetryRepository_SQL : BaseSqlConfigurationRepo<RetryEntity>, IRetryRepository 
{
    private readonly ILogger<BaseSqlConfigurationRepo<RetryEntity>> _logger;
    public RetryRepository_SQL(ILogger<BaseSqlConfigurationRepo<RetryEntity>> logger, DbContext dbContext) : base(logger, dbContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
