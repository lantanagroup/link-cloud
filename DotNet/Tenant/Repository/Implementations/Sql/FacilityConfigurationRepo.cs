using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Repository.Context;
using LantanaGroup.Link.Tenant.Repository.Interfaces.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Linq.Expressions;
namespace LantanaGroup.Link.Tenant.Repository.Implementations.Sql;

public class FacilityConfigurationRepo : BaseSqlConfigurationRepo<FacilityConfigModel>, IFacilityConfigurationRepo
{
    protected new readonly FacilityDbContext _dbContext;

    public FacilityConfigurationRepo(ILogger<FacilityConfigurationRepo> logger, FacilityDbContext dbContext) : base(logger, dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<(List<FacilityConfigModel>, PaginationMetadata)> SearchAsync(string? facilityName, string? facilityId, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default)
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

        var count = await query.CountAsync(cancellationToken);

        query = sortOrder switch
        {
            SortOrder.Ascending => query.OrderBy(SetSortBy<FacilityConfigModel>(sortBy)),
            SortOrder.Descending => query.OrderByDescending(SetSortBy<FacilityConfigModel>(sortBy)),
            _ => query.OrderBy(x => x.CreateDate)
        };

        var facilities = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

        var result = (facilities, metadata);

        return result;
    }

    /// <summary>
    /// Creates a sort expression for the given sortBy parameter
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="sortBy"></param>
    /// <returns></returns>
    private Expression<Func<T, object>> SetSortBy<T>(string? sortBy)
    {
        var sortKey = sortBy?.ToLower()??"" switch
        {
            "facilityid" => "FacilityId",
            "facilityname" => "FacilityName",
            "createdate" => "CreateDate",
            "modifydate" => "ModifyDate",
            _ => "createDate"
        };

        var parameter = Expression.Parameter(typeof(T), "p");
        var sortExpression = Expression.Lambda<Func<T, object>>(Expression.Convert(Expression.Property(parameter, sortKey), typeof(object)), parameter);

        return sortExpression;
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
