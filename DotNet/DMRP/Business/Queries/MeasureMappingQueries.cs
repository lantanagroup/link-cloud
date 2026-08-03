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
    public interface IMeasureMappingQueries
    {
        Task<MeasureMappingModel?> GetAsync(string id, CancellationToken cancellationToken = default);

        Task<PagedMeasureMappingDto> PagedSearchAsync(string sortBy = "Id", SortOrder sortOrder = SortOrder.Descending,
            int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);
    }

    public class MeasureMappingQueries : IMeasureMappingQueries
    {
        private readonly DmrpDbContext _context;

        public MeasureMappingQueries(DmrpDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<MeasureMappingModel?> GetAsync(string id, CancellationToken cancellationToken = default)
        {
            var entity = await _context.MeasureMappings.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

            return entity == null ? null : ToModel(entity);
        }

        public async Task<PagedMeasureMappingDto> PagedSearchAsync(string sortBy = "Id",
            SortOrder sortOrder = SortOrder.Descending, int pageSize = 10, int pageNumber = 1,
            CancellationToken cancellationToken = default)
        {
            var query = _context.MeasureMappings.AsNoTracking().AsQueryable();

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
                .Select(m => new MeasureMappingModel { Id = m.Id })
                .ToListAsync(cancellationToken);

            return new PagedMeasureMappingDto
            {
                Metadata = new PaginationMetadata(pageSize, pageNumber, total),
                Records = records
            };
        }

        private static MeasureMappingModel ToModel(MeasureMapping entity) => new() { Id = entity.Id };

        private static Expression<Func<MeasureMapping, object>> SetSortBy(string? sortBy)
        {
            var propertyInfo = typeof(MeasureMapping).GetProperty(sortBy ?? "Id",
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance) ?? typeof(MeasureMapping).GetProperty("Id")!;

            var parameter = Expression.Parameter(typeof(MeasureMapping), "p");
            var property = Expression.Property(parameter, propertyInfo);
            return Expression.Lambda<Func<MeasureMapping, object>>(Expression.Convert(property, typeof(object)), parameter);
        }
    }
}
