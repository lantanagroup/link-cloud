using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Repository.Context;
using LantanaGroup.Link.Tenant.Repository.Interfaces.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Tenant.Repository.Implementations.Sql;

public class FacilityConfigurationRepo : BaseSqlConfigurationRepo<FacilityConfigModel>, IFacilityConfigurationRepo
{
    protected new readonly FacilityDbContext _dbContext;

    public FacilityConfigurationRepo(ILogger<FacilityConfigurationRepo> logger, FacilityDbContext dbContext) : base(logger, dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<List<FacilityConfigModel>> SearchAsync(string? facilityName, string? facilityId, CancellationToken cancellationToken)
    {

        var query = _dbContext.Facilities.AsNoTracking().AsQueryable();

        if (facilityId is not null && facilityId.Length > 0)
        {
            query = query.Where(x => x.FacilityId == facilityId);
        }

        if (facilityName is not null && facilityName.Length > 0)
        {
            query = query.Where(x => x.FacilityName == facilityName);
        }

        var facilities = await query.ToListAsync(cancellationToken);

        return facilities;
    }

    public async Task<List<FacilityConfigModel>> GetAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Set<FacilityConfigModel>().ToListAsync(cancellationToken);
    }

    public async Task<FacilityConfigModel> GetAsyncByFacilityId(string facilityId, CancellationToken cancellationToken)
    {
        return await _dbContext.Facilities.FirstOrDefaultAsync(o => o.FacilityId == facilityId, cancellationToken);
    }

    public async Task<FacilityConfigModel> GetAsyncById(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Facilities.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async virtual Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Facilities.Where(g => g.Id == id).FirstOrDefaultAsync();

        if (entity is null) return false;

        _dbContext.Facilities.Remove(entity);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;

    }


    public async Task<HealthCheckResult> HealthCheck(CancellationToken cancellationToken)
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
