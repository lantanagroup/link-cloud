using Census.Domain.Entities;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;

namespace LantanaGroup.Link.Census.Domain.Queries;

public interface ICensusConfigQueries
{
    Task<CensusConfigModel?> GetAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<PagedConfigModel<CensusConfigModel>> PagedSearchAsync(SearchCensusConfigModel model, CancellationToken cancellationToken = default);
    Task<PagedConfigModel<CensusConfigModel>> SearchAsync(SearchCensusConfigModel model, CancellationToken cancellationToken = default);
}

public class CensusConfigQueries : ICensusConfigQueries
{
    private readonly CensusContext _dbContext;

    public CensusConfigQueries(CensusContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CensusConfigModel?> GetAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        return (await PagedSearchAsync(new SearchCensusConfigModel
        {
            FacilityId = facilityId,
        }, cancellationToken)).Records.SingleOrDefault();
    }
    public async Task<PagedConfigModel<CensusConfigModel>> SearchAsync(SearchCensusConfigModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        
        return await PagedSearchAsync(model, cancellationToken);
    }
    public async Task<PagedConfigModel<CensusConfigModel>> PagedSearchAsync(SearchCensusConfigModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var query = _dbContext.CensusConfigs.AsQueryable();

        if (!string.IsNullOrEmpty(model.FacilityId))
        {
            query = query.Where(c => c.FacilityId == model.FacilityId);
        }

        var total = await query.CountAsync(cancellationToken);

        query = model.SortOrder switch
        {
            SortOrder.Ascending => query.OrderBy(SetSortBy<CensusConfig>(model.SortBy)),
            SortOrder.Descending => query.OrderByDescending(SetSortBy<CensusConfig>(model.SortBy)),
            _ => query
        };

        var configs = await query
            .Skip((model.PageNumber - 1) * model.PageSize)
            .Take(model.PageSize)
            .Select(c => new CensusConfigModel
            {
               Id = c.Id,
               FacilityId = c.FacilityId,
               Enabled = c.Enabled,
               ScheduledTrigger = c.ScheduledTrigger,
               CreateDate = c.CreateDate,
               ModifyDate = c.ModifyDate
            })
            .ToListAsync(cancellationToken);

        return new PagedConfigModel<CensusConfigModel>
        {
            Metadata = new PaginationMetadata
            {
                PageNumber = model.PageNumber,
                PageSize = model.PageSize,
                TotalCount = total,
                TotalPages = (long)Math.Ceiling(total / (double)model.PageSize)
            },
            Records = configs
        };
    }

    private Expression<Func<T, object>> SetSortBy<T>(string? sortBy)
    {
        var type = typeof(T);
        var inputSortBy = sortBy?.Trim();
        string sortKey = "Id"; // default

        if (!string.IsNullOrEmpty(inputSortBy))
        {
            var prop = type.GetProperty(inputSortBy, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null)
            {
                sortKey = prop.Name;
            }
        }

        var parameter = Expression.Parameter(type, "p");
        var property = Expression.Property(parameter, sortKey);
        var converted = Expression.Convert(property, typeof(object));
        return Expression.Lambda<Func<T, object>>(converted, parameter);
    }
}