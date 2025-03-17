
using DataAcquisition.Domain;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using System.Linq.Expressions;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories;

public interface IQueryPlanManager
{
    Task<QueryPlan> GetAsync(string facilityId, Frequency type, CancellationToken cancellationToken = default);
    Task<QueryPlan> AddAsync(QueryPlan entity, CancellationToken cancellationToken = default);
    Task<QueryPlan> UpdateAsync(QueryPlan entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<List<QueryPlan>> FindAsync(Expression<Func<QueryPlan, bool>> predicate, CancellationToken cancellationToken = default);
    Task<List<string>> GetPlanNamesAsync(string facilityId, CancellationToken cancellationToken = default);
}

public class QueryPlanManager : IQueryPlanManager
{
    private readonly ILogger<QueryPlanManager> _logger;
    private readonly IDatabase _dbContext;

    public QueryPlanManager(ILogger<QueryPlanManager> logger, IDatabase database)
    {
        _logger = logger;
        _dbContext = database;
    }


    public async Task<List<QueryPlan>> FindAsync(Expression<Func<QueryPlan, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QueryPlanRepository.FindAsync(predicate, cancellationToken);
    }

    public async Task<QueryPlan> GetAsync(string facilityId, Frequency type, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QueryPlanRepository.FirstOrDefaultAsync(q => q.FacilityId == facilityId && q.Type == type, cancellationToken);

    }

    public async Task<List<string>> GetPlanNamesAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var plans = await _dbContext.QueryPlanRepository.FindAsync(q => q.FacilityId == facilityId, cancellationToken);
        return plans.Select(q => q.PlanName).Distinct().ToList();
    }

    public async Task<QueryPlan> AddAsync(QueryPlan entity, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QueryPlanRepository.AddAsync(entity, cancellationToken);
    }

    public async Task<QueryPlan> UpdateAsync(QueryPlan entity, CancellationToken cancellationToken = default)
    {
        var existingQueryPlan = await _dbContext.QueryPlanRepository.FirstOrDefaultAsync(q => q.FacilityId == entity.FacilityId && q.Type == entity.Type, cancellationToken);

        entity.ModifyDate = DateTime.UtcNow;

        if (existingQueryPlan != null)
        {
            existingQueryPlan.InitialQueries = entity.InitialQueries;
            existingQueryPlan.SupplementalQueries = entity.SupplementalQueries;
            existingQueryPlan.PlanName = entity.PlanName;
            existingQueryPlan.Type = entity.Type;
            existingQueryPlan.EHRDescription = entity.EHRDescription;
            existingQueryPlan.LookBack = entity.LookBack;
            existingQueryPlan.ModifyDate = entity.ModifyDate;
           return await _dbContext.QueryPlanRepository.UpdateAsync(existingQueryPlan, cancellationToken);
        }

        throw new NotFoundException($"No Query Plan for FacilityId {entity.FacilityId} was found.");
    }

    public async Task DeleteAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var entity =
            await _dbContext.QueryPlanRepository.SingleOrDefaultAsync(q => q.FacilityId == facilityId,
                cancellationToken);

        if (entity != null)
        {
            await _dbContext.QueryPlanRepository.RemoveAsync(entity);
        }
        else
        {
            throw new NotFoundException($"No Query Plan for FacilityId {entity.FacilityId} was found.");
        }
    }
}
