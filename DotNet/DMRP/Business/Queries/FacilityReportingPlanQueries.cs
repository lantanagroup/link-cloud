using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Data.Repository;
using LantanaGroup.Link.DMRP.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;

namespace LantanaGroup.Link.DMRP.Business.Queries
{
    public interface IFacilityReportingPlanQueries
    {
        Task<FacilityReportingPlanModel?> GetAsync(string id, CancellationToken cancellationToken = default);

        Task<PagedFacilityReportingPlanDto> PagedSearchAsync(string sortBy = "Id", SortOrder sortOrder = SortOrder.Descending,
            int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);
    }

    public class FacilityReportingPlanQueries : IFacilityReportingPlanQueries
    {
        private readonly DmrpDbContext _context;

        public FacilityReportingPlanQueries(DmrpDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<FacilityReportingPlanModel?> GetAsync(string id, CancellationToken cancellationToken = default)
        {
            var entity = await _context.FacilityReportingPlans.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

            return entity == null ? null : ToModel(entity);
        }

        public async Task<PagedFacilityReportingPlanDto> PagedSearchAsync(string sortBy = "Id",
            SortOrder sortOrder = SortOrder.Descending, int pageSize = 10, int pageNumber = 1,
            CancellationToken cancellationToken = default)
        {
            var query = _context.FacilityReportingPlans.AsNoTracking().AsQueryable();

            var total = await query.CountAsync(cancellationToken);

            var sortExpression = SetSortBy(sortBy);
            query = sortOrder switch
            {
                SortOrder.Ascending => query.OrderBy(sortExpression),
                _ => query.OrderByDescending(sortExpression)
            };

            var records = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new FacilityReportingPlanModel { Id = p.Id })
                .ToListAsync(cancellationToken);

            return new PagedFacilityReportingPlanDto
            {
                Metadata = new PaginationMetadata(pageSize, pageNumber, total),
                Records = records
            };
        }

        private static FacilityReportingPlanModel ToModel(FacilityReportingPlan entity) => new() { Id = entity.Id };

        private static Expression<Func<FacilityReportingPlan, object>> SetSortBy(string? sortBy)
        {
            var propertyInfo = typeof(FacilityReportingPlan).GetProperty(sortBy ?? "Id",
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance) ?? typeof(FacilityReportingPlan).GetProperty("Id")!;

            var parameter = Expression.Parameter(typeof(FacilityReportingPlan), "p");
            var property = Expression.Property(parameter, propertyInfo);
            return Expression.Lambda<Func<FacilityReportingPlan, object>>(Expression.Convert(property, typeof(object)), parameter);
        }
    }
}
