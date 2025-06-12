using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IQueryPlanManager
{
    Task<QueryPlan> GetAsync(string facilityId, Frequency type, CancellationToken cancellationToken = default);
    Task<QueryPlan> AddAsync(QueryPlan entity, CancellationToken cancellationToken = default);
    Task<QueryPlan> UpdateAsync(QueryPlan entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(string facilityId, Frequency type, CancellationToken cancellationToken = default);
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
        return await _dbContext.QueryPlanRepository.FindAsync(predicate);
    }

    public async Task<QueryPlan> GetAsync(string facilityId, Frequency type, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QueryPlanRepository.FirstOrDefaultAsync(q => q.FacilityId == facilityId && q.Type == type);

    }

    public async Task<List<string>> GetPlanNamesAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var plans = await _dbContext.QueryPlanRepository.FindAsync(q => q.FacilityId == facilityId);
        return plans.Select(q => q.PlanName).Distinct().ToList();
    }

    public async Task<QueryPlan> AddAsync(QueryPlan entity, CancellationToken cancellationToken = default)
    {
        if (entity == null)
        {
            throw new ArgumentNullException(nameof(entity), "QueryPlan entity cannot be null.");
        }

        entity.Id = Guid.NewGuid().ToString();
        entity.CreateDate = DateTime.UtcNow;
        entity.ModifyDate = DateTime.UtcNow;


        entity = await _dbContext.QueryPlanRepository.AddAsync(entity);
        await _dbContext.QueryPlanRepository.SaveChangesAsync();
        return entity;
    }

    public async Task<QueryPlan> UpdateAsync(QueryPlan entity, CancellationToken cancellationToken = default)
    {
        var existingQueryPlan = await _dbContext.QueryPlanRepository.FirstOrDefaultAsync(q => q.FacilityId == entity.FacilityId && q.Type == entity.Type);

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

            await _dbContext.QueryPlanRepository.SaveChangesAsync();

            return existingQueryPlan;
        }

        throw new NotFoundException($"No Query Plan for FacilityId {entity.FacilityId} was found.");
    }

    public async Task DeleteAsync(string facilityId, Frequency type, CancellationToken cancellationToken = default)
    {
        var entity =
            await _dbContext.QueryPlanRepository.SingleOrDefaultAsync(q => q.FacilityId == facilityId && q.Type == type);

        if (entity != null)
        {
            _dbContext.QueryPlanRepository.Remove(entity);
            await _dbContext.QueryPlanRepository.SaveChangesAsync();
        }
        else
        {
            throw new NotFoundException($"No Query Plan for FacilityId {entity.FacilityId} was found.");
        }
    }
}
