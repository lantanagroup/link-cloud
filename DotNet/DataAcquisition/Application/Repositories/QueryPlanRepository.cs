using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Interfaces;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories;

public class QueryPlanRepository : BaseSqlConfigurationRepo<QueryPlan>, IQueryPlanRepository
{
    private new readonly ILogger<QueryPlanRepository> _logger;
    private new readonly DataAcquisitionDbContext _dbContext;

    public QueryPlanRepository(ILogger<QueryPlanRepository> logger, DataAcquisitionDbContext dbContext) : base(logger, dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public override async Task<QueryPlan?> GetAsync(string id, CancellationToken cancellationToken)
    {
        return await _dbContext.QueryPlan.FirstOrDefaultAsync(q => q.FacilityId == id, cancellationToken);
    }

    public async Task<List<QueryPlan>> GetQueryPlansByFacilityId(string facilityId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QueryPlan.Where(x => x.FacilityId == facilityId).ToListAsync(cancellationToken);
    }

    public async Task<QueryPlan?> GetByFacilityAndReportAsync(string facilityId, string reportType, CancellationToken cancellationToken = default)
    {       
        return await _dbContext.QueryPlan.FirstOrDefaultAsync(x => x.FacilityId == facilityId && x.ReportType == reportType, cancellationToken);
    }

    public override async Task<QueryPlan> AddAsync(QueryPlan entity, CancellationToken cancellationToken = default)
    {
        entity.Id = Guid.NewGuid();
        return (await _dbContext.QueryPlan.AddAsync(entity, cancellationToken)).Entity;
    }

    public override async Task<QueryPlan> UpdateAsync(QueryPlan entity, CancellationToken cancellationToken = default)
    {
        var existingQueryPlan = await GetAsync(entity.FacilityId, cancellationToken);

        entity.ModifyDate = DateTime.UtcNow;

        if (existingQueryPlan != null)
        {
            existingQueryPlan.InitialQueries = entity.InitialQueries;
            existingQueryPlan.SupplementalQueries = entity.SupplementalQueries;
            existingQueryPlan.PlanName = entity.PlanName;
            existingQueryPlan.ReportType = entity.ReportType;
            existingQueryPlan.EHRDescription = entity.EHRDescription;
            existingQueryPlan.LookBack = entity.LookBack;
            existingQueryPlan.ModifyDate = entity.ModifyDate;
            _dbContext.QueryPlan.Update(existingQueryPlan);
        }
        else
        {
            throw new NotFoundException($"No Query Plan for FacilityId {entity.FacilityId} was found.");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return existingQueryPlan;
    }

    public override async Task DeleteAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.QueryPlan.FirstOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);
        if (entity != null)
        {
            _dbContext.QueryPlan.Remove(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteSupplementalQueriesForFacility(string facilityId, CancellationToken cancellationToken = default) 
    {
        var entity = await _dbContext.QueryPlan.FirstOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);
        if (entity != null)
        {
            entity.SupplementalQueries = null;
            _dbContext.QueryPlan.Update(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteInitialQueriesForFacility(string facilityId, CancellationToken cancellationToken = default) 
    {
        var entity = await _dbContext.QueryPlan.FirstOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);
        if (entity != null)
        {
            entity.InitialQueries = null;
            _dbContext.QueryPlan.Update(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task SaveInitialQueries(string facilityId, Dictionary<string, IQueryConfig> config, CancellationToken cancellationToken)
    {
        var queryResult = await _dbContext.QueryPlan.FirstOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);
        if (queryResult != null)
        {
            queryResult.InitialQueries = config;
            _dbContext.QueryPlan.Update(queryResult);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task SaveSupplementalQueries(string facilityId, Dictionary<string, IQueryConfig> config, CancellationToken cancellationToken)
    {
        var queryResult = await _dbContext.QueryPlan.FirstOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);
        
        if (queryResult != null)
        {
            queryResult.SupplementalQueries = config;
            _dbContext.QueryPlan.Update(queryResult);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public void Dispose() { }
}
