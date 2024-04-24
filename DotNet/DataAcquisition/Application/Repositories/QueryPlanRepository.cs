using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Interfaces;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories;

public class QueryPlanRepository : BaseSqlConfigurationRepo<QueryPlan>, IQueryPlanRepository
{
    private readonly ILogger<QueryPlanRepository> _logger;
    private readonly DataAcquisitionDbContext _dbContext;

    public QueryPlanRepository(ILogger<QueryPlanRepository> logger, DataAcquisitionDbContext dbContext) : base(logger, dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public override async Task<QueryPlan> GetAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QueryPlan.FirstOrDefaultAsync(x => x.FacilityId == facilityId);
    }

    public async Task<List<QueryPlan>> GetQueryPlansByFacilityId(string facilityId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QueryPlan.Where(x => x.FacilityId == facilityId).ToListAsync();
    }

    public async Task<QueryPlan> GetByFacilityAndReportAsync(string facilityId, string reportType, CancellationToken cancellationToken = default)
    {       
        return await _dbContext.QueryPlan.FirstOrDefaultAsync(x => x.FacilityId == facilityId && x.ReportType == reportType);
    }

    public override async Task<QueryPlan> UpdateAsync(QueryPlan Entity, CancellationToken cancellationToken = default)
    {
        var existingQueryPlan = await GetAsync(Entity.FacilityId, cancellationToken);

        Entity.ModifyDate = DateTime.UtcNow;

        if (existingQueryPlan != null)
        {
            Entity.Id = existingQueryPlan.Id;
            Entity.CreateDate = existingQueryPlan.CreateDate;
            _dbContext.QueryPlan.Update(Entity);
        }
        else
        {
            Entity.Id = Guid.NewGuid();
            Entity.CreateDate = DateTime.UtcNow;
            await _dbContext.QueryPlan.AddAsync(Entity);
        }

        await _dbContext.SaveChangesAsync();

        return Entity;
    }

    public override async Task DeleteAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.QueryPlan.FirstOrDefaultAsync(x => x.FacilityId == facilityId);
        if (entity != null)
        {
            _dbContext.QueryPlan.Remove(entity);
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task DeleteSupplementalQueriesForFacility(string facilityId, CancellationToken cancellationToken = default) 
    {
        var entity = await _dbContext.QueryPlan.FirstOrDefaultAsync(x => x.FacilityId == facilityId);
        if (entity != null)
        {
            entity.SupplementalQueries = null;
            _dbContext.QueryPlan.Update(entity);
            await _dbContext.SaveChangesAsync();
        }
    }
    public async Task DeleteInitialQueriesForFacility(string facilityId, CancellationToken cancellationToken = default) 
    {
        var entity = await _dbContext.QueryPlan.FirstOrDefaultAsync(x => x.FacilityId == facilityId);
        if (entity != null)
        {
            entity.InitialQueries = null;
            _dbContext.QueryPlan.Update(entity);
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task SaveInitialQueries(string facilityId, Dictionary<string, IQueryConfig> config, CancellationToken cancellationToken)
    {
        var queryResult = await _dbContext.QueryPlan.FirstOrDefaultAsync(x => x.FacilityId == facilityId);
        if (queryResult != null)
        {
            queryResult.InitialQueries = config;
            _dbContext.QueryPlan.Update(queryResult);
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task SaveSupplementalQueries(string facilityId, Dictionary<string, IQueryConfig> config, CancellationToken cancellationToken)
    {
        var queryResult = await _dbContext.QueryPlan.FirstOrDefaultAsync(x => x.FacilityId == facilityId);
        
        if (queryResult != null)
        {
            queryResult.SupplementalQueries = config;
            _dbContext.QueryPlan.Update(queryResult);
            await _dbContext.SaveChangesAsync();
        }
    }

    public void Dispose() { }
}
