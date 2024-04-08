using Census.Domain.Entities;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;
using static LantanaGroup.Link.Census.Application.Settings.CensusConstants;

namespace LantanaGroup.Link.Census.Application.Repositories;

public class CensusConfigRepository : BaseSqlConfigurationRepo<CensusConfigEntity>, ICensusConfigRepository, IDisposable
{
    private readonly ILogger<CensusConfigRepository> _logger;
    private readonly CensusContext _dbContext;

    public CensusConfigRepository(ILogger<CensusConfigRepository> logger, CensusContext dbContext) : base(logger, dbContext)
    {
    }

    public async Task<CensusConfigEntity> GetByFacilityIdAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.CensusConfigs.FirstOrDefaultAsync(c => c.FacilityID == facilityId, cancellationToken);
    }

    public async Task<bool> RemoveByFacilityIdAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.CensusConfigs.FirstOrDefaultAsync(c => c.FacilityID == facilityId, cancellationToken);

        if (entity is null) return false;

        _dbContext.CensusConfigs.Remove(entity);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> HealthCheck()
    {
        try
        {
            bool outcome = await _dbContext.Database.CanConnectAsync();

            return outcome ? true : false;
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(CensusLoggingIds.HealthCheck, "Census Service - Database Health Check"), ex, "Health check failed for database connection.");
            return false;
        }
    }

    public async Task<IEnumerable<CensusConfigEntity>> GetAllFacilities(CancellationToken cancellationToken = default)
    {
        return await _dbContext.CensusConfigs.ToListAsync(cancellationToken);
    }

    public void Dispose()
    {
        base._dbContext.Dispose();
    }
}
