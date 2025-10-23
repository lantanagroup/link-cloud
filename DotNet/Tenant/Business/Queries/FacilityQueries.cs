using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Business.Models;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;

namespace LantanaGroup.Link.Tenant.Business.Queries
{
    public interface IFacilityQueries
    {
        Task<FacilityModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);
        Task<FacilityModel?> GetAsync(string facilityId, string? facilityName = null, CancellationToken cancellationToken = default);
        Task<List<FacilityModel>> SearchAsync(FacilitySearchModel model, CancellationToken cancellationToken = default);
        Task<PagedConfigModel<FacilityModel>> PagedSearchAsync(FacilitySearchModel model, string sortBy = "FacilityName", SortOrder sortOrder = SortOrder.Descending, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);
    }

    public class FacilityQueries : IFacilityQueries
    {
        private readonly TenantDbContext _context;
        private readonly IEntityRepository<Facility> _entityRepository;

        public FacilityQueries(TenantDbContext context, IEntityRepository<Facility> repository)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _entityRepository = repository;
        }

        public async Task<FacilityModel?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return (await SearchAsync(new FacilitySearchModel { Id = id }, cancellationToken)).SingleOrDefault();   
        }

        public async Task<FacilityModel?> GetAsync(string facilityId, string? facilityName = null, CancellationToken cancellationToken = default)
        {
            return (await SearchAsync(new FacilitySearchModel {  FacilityId = facilityId, FacilityName = facilityName }, cancellationToken)).SingleOrDefault();
        }

        public async Task<List<FacilityModel>> SearchAsync(FacilitySearchModel model, CancellationToken cancellationToken = default)
        {
            var result = await PagedSearchAsync(model, pageSize: int.MaxValue, cancellationToken: cancellationToken);
            return result.Records;
        }

        public async Task<PagedConfigModel<FacilityModel>> PagedSearchAsync(FacilitySearchModel model, string sortBy = "FacilityId", SortOrder sortOrder = SortOrder.Descending, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default)
        {
            var query = _context.Facilities.AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrEmpty(model.FacilityId))
            {
                query = query.Where(log => log.FacilityId == model.FacilityId);
            }

            if (!string.IsNullOrEmpty(model.FacilityName))
            {
                if (model.FacilityNameContains == true)
                {
                    query = query.Where(log => log.FacilityName.Contains(model.FacilityName));
                }
                else
                {
                    query = query.Where(log => log.FacilityName == model.FacilityName);
                }
            }

            if (model.Id != null)
            {
                query = query.Where(log => log.Id == model.Id);
            }

            var totalRecords = await query.CountAsync(cancellationToken);

            query = sortOrder switch
            {
                SortOrder.Ascending => query.OrderBy(SetSortBy<Facility>(sortBy)),
                SortOrder.Descending => query.OrderByDescending(SetSortBy<Facility>(sortBy)),
                _ => query
            };

            var total = await query.CountAsync();

            var facilities = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(f => new FacilityModel
                {
                    Id = f.Id,
                    FacilityId = f.FacilityId,
                    FacilityName = f.FacilityName,
                    TimeZone = f.TimeZone,
                    ScheduledReports = new TenantScheduledReportConfig
                    {
                        Daily = f.ScheduledReports.Daily,
                        Weekly = f.ScheduledReports.Weekly,
                        Monthly = f.ScheduledReports.Monthly
                    },
                }).ToListAsync(cancellationToken);

            return new PagedConfigModel<FacilityModel>
            {
                Metadata = new PaginationMetadata
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = total,
                    TotalPages = (long)Math.Ceiling((double)total / pageSize),
                },
                Records = facilities
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
}
