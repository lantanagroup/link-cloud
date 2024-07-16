using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace LantanaGroup.Link.Shared.Application.Repositories.Implementations;

public class EntityRepository<T> : IEntityRepository<T> where T : BaseEntity
{
    protected readonly ILogger _logger;

    protected readonly DbContext _dbContext;

    public EntityRepository(ILogger<EntityRepository<T>> logger, DbContext dbContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken)
    {
        entity.Id = Guid.NewGuid().ToString();

        var result = (await _dbContext.Set<T>().AddAsync(entity, cancellationToken)).Entity;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    public virtual async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken)
    {
        entity = _dbContext.Set<T>().Update(entity).Entity;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return entity;

    }

    public virtual async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Set<T>().FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (entity is null) return;

        _dbContext.Set<T>().Remove(entity);

        await _dbContext.SaveChangesAsync(cancellationToken);

    }

    public virtual T Add(T entity)
    {
        entity.Id = Guid.NewGuid().ToString();

        var result = _dbContext.Set<T>().Add(entity).Entity;

        _dbContext.SaveChanges();
        
        return result;
    }

    public virtual T Get(string id)
    {
        return _dbContext.Set<T>().SingleOrDefault(o => o.Id == id);
    }

    public virtual async Task<T> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<T>().SingleOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public virtual async Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<T>().ToListAsync(cancellationToken);
    }

    public virtual T Update(T entity)
    {
        _dbContext.Set<T>().Update(entity);

        _dbContext.SaveChanges();

        return entity;
    }

    public virtual void Delete(string id)
    {
        var entity = _dbContext.Set<T>().FirstOrDefault(g => g.Id == id);

        if (entity is null) return;

        _dbContext.Set<T>().Remove(entity);

        _dbContext.SaveChanges();
    }

    public async Task RemoveAsync(T entity)
    {
        _dbContext.Remove(entity);

        await _dbContext.SaveChangesAsync();
    }

    public async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Set<T>().AsQueryable();

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<T>().FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task<T> FirstAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<T>().FirstAsync(predicate, cancellationToken);
    }

    public async Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<T>().SingleOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task<T> SingleAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<T>().SingleAsync(predicate, cancellationToken);
    }

    public async Task<HealthCheckResult> HealthCheck(int eventId)
    {
        try
        {
            bool outcome = await _dbContext.Database.CanConnectAsync();

            if (outcome)
            {
                return HealthCheckResult.Healthy();
            }
            else
            {
                return HealthCheckResult.Unhealthy();
            }

        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(exception: ex);
        }
    }
}
