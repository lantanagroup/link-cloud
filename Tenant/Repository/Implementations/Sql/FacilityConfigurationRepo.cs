using Google.Api;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Repository.Context;
using LantanaGroup.Link.Tenant.Repository.Interfaces.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;
using Tenant;

namespace LantanaGroup.Link.Tenant.Repository.Implementations.Sql;

public class FacilityConfigurationRepo : BaseConfigurationRepo<FacilityConfigModel>, IFacilityConfigurationRepo
{
    private readonly ILogger<FacilityConfigurationRepo> _logger;
    protected new readonly FacilityDbContext _dbContext;

    public FacilityConfigurationRepo(ILogger<FacilityConfigurationRepo> logger, FacilityDbContext dbContext) : base( logger, dbContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<List<FacilityConfigModel>> SearchAsync(string? facilityName, string? facilityId, CancellationToken cancellationToken)
    {

        var query = _dbContext.Facilities.AsNoTracking().AsQueryable();
     #region Build Query

        if (facilityId is not null && facilityId.Length > 0)
        {
            query = query.Where(x => x.FacilityId == facilityId);
        }

        if (facilityName is not null && facilityName.Length > 0)
        {
            query = query.Where(x => x.FacilityName == facilityName);
        }

        #endregion
        var facilities = await query.ToListAsync(cancellationToken);

        return facilities;
    }

    public async Task<FacilityConfigModel> GetAsyncByFacilityId(string facilityId, CancellationToken cancellationToken)
    {
        return await _dbContext.Facilities.FirstOrDefaultAsync(o => o.FacilityId == facilityId, cancellationToken);
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
