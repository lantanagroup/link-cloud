using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.Shared.Application.Repositories.Implementations;

public class BaseSqlConfigurationRepo<T> : ISqlPersistenceRepository<T> where T : SqlBaseEntity
{
    protected readonly ILogger _logger;

    protected readonly DbContext _dbContext;

    public BaseSqlConfigurationRepo(ILogger<BaseSqlConfigurationRepo<T>> logger, DbContext dbContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<List<T>> GetAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Set<T>().ToListAsync(cancellationToken);
    }

    public async Task<T> GetAsyncById(string id, CancellationToken cancellationToken)
    {
        return await _dbContext.Set<T>().FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async virtual Task<bool> CreateAsync(T entity, CancellationToken cancellationToken)
    {
        _dbContext.Set<T>().Add(entity);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }


    public async virtual Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken)
    {
        _dbContext.Set<T>().Update(entity);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;

    }

    public async virtual Task<bool> RemoveAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Set<T>().Where(g => g.Id == id).FirstOrDefaultAsync();

        if (entity is null) return false;
        
        _dbContext.Set<T>().Remove(entity);
  
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;

    }


}
