using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Domain.Entities;

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

    public async Task<T> GetAsync(string id, CancellationToken cancellationToken)
    {
        return await _dbContext.Set<T>().FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async virtual Task AddAsync(T entity, CancellationToken cancellationToken)
    {

        _dbContext.Set<T>().Add(entity);

        await _dbContext.SaveChangesAsync(cancellationToken);

    }

    public async virtual Task<T> UpdateAsync(T entity, CancellationToken cancellationToken)
    {
        _dbContext.Set<T>().Update(entity);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return entity;

    }

    public async virtual Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Set<T>().Where(g => g.Id == id).FirstOrDefaultAsync();

        if (entity is null) return;

        _dbContext.Set<T>().Remove(entity);

        await _dbContext.SaveChangesAsync(cancellationToken);

    }

    public void Add(T entity)
    {
        throw new NotImplementedException();
    }

    public T Get(string id)
    {
        throw new NotImplementedException();
    }


    public T Update(T entity)
    {
        throw new NotImplementedException();
    }

    public void Delete(string id)
    {
        throw new NotImplementedException();
    }

}
