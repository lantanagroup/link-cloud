using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories;

public class QueryPlanRepository : EntityRepository<QueryPlan>
{
    private new readonly ILogger<QueryPlanRepository> _logger;
    private new readonly DataAcquisitionDbContext _dbContext;

    public QueryPlanRepository(ILogger<QueryPlanRepository> logger, DataAcquisitionDbContext dbContext) : base(logger, dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public override async Task<QueryPlan> GetAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QueryPlan.FirstOrDefaultAsync(q => q.FacilityId == facilityId, cancellationToken);
    }

    public override async Task<QueryPlan> UpdateAsync(QueryPlan entity, CancellationToken cancellationToken = default)
    {
        var existingQueryPlan = (await FindAsync(q => q.FacilityId == entity.FacilityId && q.ReportType == entity.ReportType)).FirstOrDefault();

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
        var entity = (await FindAsync(q =>  q.FacilityId == facilityId)).FirstOrDefault();
        if (entity != null)
        {
            await Remove(entity);
        }
    }
}
