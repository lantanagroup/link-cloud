using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Reflection;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;

public interface IReferenceResourcesQueries
{
    Task<ReferenceResourcesModel?> GetAsync(string resourceId, string facilityId, CancellationToken cancellationToken = default);
    Task<PagedConfigModel<ReferenceResourcesModel>> SearchAsync(SearchReferenceResourcesModel request, CancellationToken cancellationToken = default);
}

public class ReferenceResourcesQueries : IReferenceResourcesQueries
{
    private readonly IDatabase _database;
    private readonly DataAcquisitionDbContext _dbContext;
    private readonly ILogger<ReferenceResourcesQueries> _logger;

    public ReferenceResourcesQueries(IDatabase database, DataAcquisitionDbContext dbContext, ILogger<ReferenceResourcesQueries> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ReferenceResourcesModel?> GetAsync(string resourceId, string facilityId, CancellationToken cancellationToken = default)
    {
        var entity = await _database.ReferenceResourcesRepository.FirstOrDefaultAsync(x => x.ResourceId == resourceId && x.FacilityId == facilityId, cancellationToken);
        return entity != null ? ReferenceResourcesModel.FromDomain(entity) : null;
    }

    public async Task<PagedConfigModel<ReferenceResourcesModel>> SearchAsync(SearchReferenceResourcesModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var query = _dbContext.ReferenceResources.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(model.FacilityId))
        {
            query = query.Where(r => r.FacilityId == model.FacilityId);
        }

        if (!string.IsNullOrEmpty(model.ResourceId))
        {
            query = query.Where(r => r.ResourceId == model.ResourceId);
        }

        if (model.ResourceIds != null && model.ResourceIds.Any())
        {
            query = query.Where(r => model.ResourceIds.Contains(r.ResourceId));
        }

        if (!string.IsNullOrEmpty(model.ResourceType))
        {
            query = query.Where(r => r.ResourceType == model.ResourceType);
        }

        if (model.QueryPhase.HasValue)
        {
            query = query.Where(r => r.QueryPhase == model.QueryPhase.Value);
        }

        if (model.DataAcquisitionLogId.HasValue)
        {
            query = query.Where(r => r.DataAcquisitionLogId == model.DataAcquisitionLogId.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        query = model.SortOrder switch
        {
            SortOrder.Ascending => query.OrderBy(SetSortBy<ReferenceResources>(model.SortBy)),
            SortOrder.Descending => query.OrderByDescending(SetSortBy<ReferenceResources>(model.SortBy)),
            _ => query
        };

        var resources = await query
            .Skip((model.PageNumber - 1) * model.PageSize)
            .Take(model.PageSize)
            .Select(r => ReferenceResourcesModel.FromDomain(r))
            .ToListAsync(cancellationToken);

        return new PagedConfigModel<ReferenceResourcesModel>
        {
            Metadata = new PaginationMetadata
            {
                PageNumber = model.PageNumber,
                PageSize = model.PageSize,
                TotalCount = total,
                TotalPages = (long)Math.Ceiling(total / (double)model.PageSize)
            },
            Records = resources
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