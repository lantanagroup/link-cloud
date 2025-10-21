using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace LantanaGroup.Link.Shared.Domain.Repositories.Implementations;

public class BaseEntityRepository<T, TDbContext> : IBaseEntityRepository<T>
    where T : BaseEntity
    where TDbContext : DbContext
{
    protected readonly ILogger _logger;

    protected readonly TDbContext _dbContext;

    public BaseEntityRepository(ILogger<BaseEntityRepository<T, TDbContext>> logger, TDbContext dbContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<T>().AnyAsync(predicate, cancellationToken);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.Id ??= Guid.NewGuid().ToString();

        var result = (await _dbContext.Set<T>().AddAsync(entity, cancellationToken)).Entity;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    public virtual async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity = _dbContext.Set<T>().Update(entity).Entity;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return entity;

    }

    public virtual async Task DeleteAsync(T? entity, CancellationToken cancellationToken = default)
    {
        if (entity is null) return;

        _dbContext.Set<T>().Remove(entity);

        await _dbContext.SaveChangesAsync(cancellationToken);

    }

    public virtual async Task DeleteAsync(object id, CancellationToken cancellationToken = default)
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

    public virtual T Get(object id)
    {
        return _dbContext.Set<T>().SingleOrDefault(o => o.Id == id);
    }

    public virtual async Task<T> GetAsync(object id, CancellationToken cancellationToken = default)
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

    public virtual void Delete(object id)
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

    public async Task<(List<T>, PaginationMetadata)> SearchAsync(Expression<Func<T, bool>> predicate, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default)
    {

        var query = _dbContext.Set<T>().AsNoTracking().AsQueryable();

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        var count = await query.CountAsync(cancellationToken);

        if (sortOrder != null)
        {
            query = sortOrder switch
            {
                SortOrder.Ascending => query.OrderBy(SetSortBy<T>(sortBy)),
                SortOrder.Descending => query.OrderByDescending(SetSortBy<T>(sortBy))
            };
        }

        var results = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

        var result = (results, metadata);

        return result;
    }

    public virtual void StartTransaction()
    {
        if (_dbContext.Database.CurrentTransaction != null)
            throw new InvalidOperationException("A transaction is already in progress.");
        _dbContext.Database.BeginTransaction();
    }

    public virtual async Task StartTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.CurrentTransaction != null)
            throw new InvalidOperationException("A transaction is already in progress.");
        await _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public virtual void CommitTransaction()
    {
        if (_dbContext.Database.CurrentTransaction == null)
            throw new InvalidOperationException("No transaction is in progress.");
        _dbContext.Database.CommitTransaction();
    }

    public virtual async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.CurrentTransaction == null)
            throw new InvalidOperationException("No transaction is in progress.");
        await _dbContext.Database.CommitTransactionAsync(cancellationToken);
    }

    public virtual void RollbackTransaction()
    {
        if (_dbContext.Database.CurrentTransaction == null)
            throw new InvalidOperationException("No transaction is in progress.");
        _dbContext.Database.RollbackTransaction();
    }

    public virtual async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.CurrentTransaction == null)
            throw new InvalidOperationException("No transaction is in progress.");
        await _dbContext.Database.RollbackTransactionAsync(cancellationToken);
    }

    private Expression<Func<T, object>> SetSortBy<T>(string? sortBy)
    {
        var sortKey = sortBy?.ToLower() ?? "";
        var parameter = Expression.Parameter(typeof(T), "p");
        var sortExpression = Expression.Lambda<Func<T, object>>(Expression.Convert(Expression.Property(parameter, sortKey), typeof(object)), parameter);

        return sortExpression;
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
