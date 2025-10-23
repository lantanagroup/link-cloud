using Census.Domain.Entities;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace LantanaGroup.Link.Census.Domain.Repositories;

public interface IDatabase
{
    IEntityRepository<CensusConfigEntity> CensusConfigRepository { get; set; }

    Task SaveChangesAsync();
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken token);
    Task CommitTransactionAsync(CancellationToken token);
    Task RollbackTransactionAsync(CancellationToken token);
}
public class Database : IDatabase
{
    private readonly CensusContext _dbContext;
    public IEntityRepository<CensusConfigEntity> CensusConfigRepository { get; set; }

    public Database(
        CensusContext context,
        IEntityRepository<CensusConfigEntity> queryConfigurationRepository)
    {
        _dbContext = context;
        CensusConfigRepository = queryConfigurationRepository;
    }

    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken token)
    {
        return await _dbContext.Database.BeginTransactionAsync(token);
    }

    public async Task CommitTransactionAsync(CancellationToken token)
    {
        await _dbContext.Database.CommitTransactionAsync(token);
    }

    public async Task RollbackTransactionAsync(CancellationToken token)
    {
        await _dbContext.Database.RollbackTransactionAsync(token);
    }
}