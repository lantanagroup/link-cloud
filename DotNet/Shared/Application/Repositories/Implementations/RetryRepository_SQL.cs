using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.Shared.Application.Repositories.Implementations;
public class RetryRepository_SQL : BaseSqlConfigurationRepo<RetryEntity>, IRetryRepository 
{
    private readonly ILogger<BaseSqlConfigurationRepo<RetryEntity>> _logger;
    private readonly DbContext _context;
    public RetryRepository_SQL(ILogger<BaseSqlConfigurationRepo<RetryEntity>> logger, DbContext dbContext) : base(logger, dbContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public override void Add(RetryEntity entity)
    {
        entity.Id = Guid.NewGuid().ToString();
        base.Add(entity);
    }

    public override Task AddAsync(RetryEntity entity, CancellationToken cancellationToken)
    {
        entity.Id = Guid.NewGuid().ToString();
        return base.AddAsync(entity, cancellationToken);
    }

    public Task<List<RetryEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _context.Set<RetryEntity>().ToListAsync(cancellationToken);
    }


}
