using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Domain.Entities;
using System.Threading;

namespace LantanaGroup.Link.Shared.Application.Repositories.Implementations;

public class BaseSqlConfigurationRepo<T> : IPersistenceRepository<T> where T : BaseEntity
{
    protected readonly ILogger _logger;

    protected readonly DbContext _dbContext;

    public BaseSqlConfigurationRepo(ILogger<BaseSqlConfigurationRepo<T>> logger, DbContext dbContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public virtual async Task<T> GetAsync(string id, CancellationToken cancellationToken)
    {
        return await _dbContext.Set<T>().FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken)
    {

        var result = (await _dbContext.Set<T>().AddAsync(entity, cancellationToken)).Entity;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    public virtual async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken)
    {
        _dbContext.Set<T>().Update(entity);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return entity;

    }

    public virtual async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Set<T>().Where(g => g.Id == id).FirstOrDefaultAsync();

        if (entity is null) return;

        _dbContext.Set<T>().Remove(entity);

        await _dbContext.SaveChangesAsync(cancellationToken);

    }

    public virtual T Add(T entity)
    {
        var result = _dbContext.Set<T>().Add(entity).Entity;

        _dbContext.SaveChanges();
        
        return result;
    }

    public virtual T Get(string id)
    {
        return _dbContext.Set<T>().FirstOrDefault(o => o.Id == id);
    }


    public virtual T Update(T entity)
    {
        _dbContext.Set<T>().Update(entity);

        _dbContext.SaveChanges();

        return entity;
    }

    public virtual void Delete(string id)
    {
        var entity = _dbContext.Set<T>().Where(g => g.Id == id).FirstOrDefault();

        if (entity is null) return;

        _dbContext.Set<T>().Remove(entity);

        _dbContext.SaveChanges();
    }

}
