using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Reflection;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;

public interface IQueryPlanQueries
{
    Task<QueryPlanModel?> GetAsync(string facilityId, Frequency type, CancellationToken cancellationToken = default);
    Task<List<QueryPlanModel>> FindAsync(Expression<Func<QueryPlan, bool>> predicate, CancellationToken cancellationToken = default);
    Task<List<string>> GetPlanNamesAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<PagedConfigModel<QueryPlanModel>> SearchAsync(SearchQueryPlanModel model, CancellationToken cancellationToken = default);
}

public class QueryPlanQueries : IQueryPlanQueries
{
    private readonly IDatabase _database;
    private readonly DataAcquisitionDbContext _dbContext;
    private readonly ILogger<QueryPlanQueries> _logger;

    public QueryPlanQueries(IDatabase database, DataAcquisitionDbContext dbContext, ILogger<QueryPlanQueries> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<QueryPlanModel?> GetAsync(string facilityId, Frequency type, CancellationToken cancellationToken = default)
    {
        var entity = await _database.QueryPlanRepository.FirstOrDefaultAsync(q => q.FacilityId == facilityId && q.Type == type);
        return entity != null ? QueryPlanModel.FromDomain(entity) : null;
    }

    public async Task<List<QueryPlanModel>> FindAsync(Expression<Func<QueryPlan, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var entities = await _database.QueryPlanRepository.FindAsync(predicate);
        return entities.Select(QueryPlanModel.FromDomain).ToList();
    }

    public async Task<List<string>> GetPlanNamesAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var plans = await _database.QueryPlanRepository.FindAsync(q => q.FacilityId == facilityId);
        return plans.Select(q => q.PlanName).Distinct().ToList();
    }

    public async Task<PagedConfigModel<QueryPlanModel>> SearchAsync(SearchQueryPlanModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var query = _dbContext.QueryPlans.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(model.FacilityId))
        {
            query = query.Where(qp => qp.FacilityId == model.FacilityId);
        }

        if (!string.IsNullOrEmpty(model.PlanName))
        {
            query = query.Where(qp => qp.PlanName == model.PlanName);
        }

        if (model.Type.HasValue)
        {
            query = query.Where(qp => qp.Type == model.Type.Value);
        }

        if (!string.IsNullOrEmpty(model.EHRDescription))
        {
            query = query.Where(qp => qp.EHRDescription.Contains(model.EHRDescription));
        }

        if (!string.IsNullOrEmpty(model.LookBack))
        {
            query = query.Where(qp => qp.LookBack == model.LookBack);
        }

        var total = await query.CountAsync(cancellationToken);

        query = model.SortOrder switch
        {
            SortOrder.Ascending => query.OrderBy(SetSortBy<QueryPlan>(model.SortBy)),
            SortOrder.Descending => query.OrderByDescending(SetSortBy<QueryPlan>(model.SortBy)),
            _ => query
        };

        var plans = await query
            .Skip((model.PageNumber - 1) * model.PageSize)
            .Take(model.PageSize)
            .Select(qp => QueryPlanModel.FromDomain(qp))
            .ToListAsync(cancellationToken);

        return new PagedConfigModel<QueryPlanModel>
        {
            Metadata = new PaginationMetadata
            {
                PageNumber = model.PageNumber,
                PageSize = model.PageSize,
                TotalCount = total,
                TotalPages = (long)Math.Ceiling(total / (double)model.PageSize)
            },
            Records = plans ?? new()
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