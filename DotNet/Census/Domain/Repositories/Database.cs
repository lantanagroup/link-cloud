using Census.Domain.Entities;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace LantanaGroup.Link.Census.Domain.Repositories;

public interface IDatabase
{
    IEntityRepository<CensusConfig> CensusConfigRepository { get; set; }
    IEntityRepository<PatientEvent> PatientEventRepository { get; set; }
    IEntityRepository<PatientEncounter> PatientEncounterRepository { get; set; }

    Task SaveChangesAsync(CancellationToken token = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken token);
    Task CommitTransactionAsync(CancellationToken token);
    Task RollbackTransactionAsync(CancellationToken token);
}
public class Database : IDatabase
{
    private readonly CensusContext _dbContext;
    public IEntityRepository<CensusConfig> CensusConfigRepository { get; set; }
    public IEntityRepository<PatientEvent> PatientEventRepository { get; set; }
    public IEntityRepository<PatientEncounter> PatientEncounterRepository { get; set; }

    public Database(
        CensusContext context,
        IEntityRepository<CensusConfig> queryConfigurationRepository,
        IEntityRepository<PatientEvent> patientEventRepository,
        IEntityRepository<PatientEncounter> patientEncounterRepository)
    {
        _dbContext = context;
        CensusConfigRepository = queryConfigurationRepository;
        PatientEventRepository = patientEventRepository;
        PatientEncounterRepository = patientEncounterRepository;
    }

    public async Task SaveChangesAsync(CancellationToken token = default)
    {
        await _dbContext.SaveChangesAsync(token);
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