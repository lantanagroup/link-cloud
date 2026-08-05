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

        Task<FacilityModel?> GetAsync(string facilityId, string? facilityName = null,
            CancellationToken cancellationToken = default, bool includeDeleted = false);

        Task<List<FacilityModel>> SearchAsync(FacilitySearchModel model, CancellationToken cancellationToken = default,
            bool includeDeleted = false);

        Task<PagedConfigModel<FacilityModel>> PagedSearchAsync(FacilitySearchModel model,
            string sortBy = "FacilityId", SortOrder sortOrder = SortOrder.Descending, int pageSize = 10,
            int pageNumber = 1, bool includeDeleted = false, CancellationToken cancellationToken = default);
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

        public async Task<FacilityModel?> GetAsync(string facilityId, string? facilityName = null,
            CancellationToken cancellationToken = default, bool includeDeleted = false)
        {
            return (await SearchAsync(new FacilitySearchModel { FacilityId = facilityId, FacilityName = facilityName },
                cancellationToken, includeDeleted)).SingleOrDefault();
        }

        public async Task<List<FacilityModel>> SearchAsync(FacilitySearchModel model,
            CancellationToken cancellationToken = default, bool includeDeleted = false)
        {
            var result = await PagedSearchAsync(model, pageSize: int.MaxValue, includeDeleted: includeDeleted, cancellationToken: cancellationToken);
            return result.Records;
        }

        public async Task<PagedConfigModel<FacilityModel>> PagedSearchAsync(
            FacilitySearchModel model,
            string sortBy = "FacilityId",
            SortOrder sortOrder = SortOrder.Descending,
            int pageSize = 10,
            int pageNumber = 1,
            bool includeDeleted = false,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Facilities.AsNoTracking().AsQueryable();

            query = query.Where(f => includeDeleted || !f.IsDeleted);

            if (!string.IsNullOrEmpty(model.FacilityId))
            {
                query = query.Where(f => f.FacilityId == model.FacilityId);
            }

            if (!string.IsNullOrEmpty(model.FacilityName))
            {
                if (model.FacilityNameContains == true)
                    query = query.Where(f => f.FacilityName.Contains(model.FacilityName));
                else
                    query = query.Where(f => f.FacilityName == model.FacilityName);
            }

            if (!string.IsNullOrEmpty(model.TimeZone))
            {
                query = query.Where(f => f.TimeZone == model.TimeZone);
            }

            if (model.Vendor != null)
            {
                if(model.Vendor.Id != null)
                    query = query.Where(f => f.VendorVersion!.Vendor!.Id == model.Vendor.Id);
                else if(!string.IsNullOrEmpty(model.Vendor.Name))
                    query = query.Where(f => f.VendorVersion!.Vendor!.Name == model.Vendor.Name);
            }

            if (model.Id != null)
            {
                query = query.Where(f => f.Id == model.Id);
            }

            var total = await query.CountAsync(cancellationToken);

            query = sortOrder switch
            {
                SortOrder.Ascending => query.OrderBy(SetSortBy<Facility>(sortBy)),
                SortOrder.Descending => query.OrderByDescending(SetSortBy<Facility>(sortBy)),
                _ => query
            };

            var facilities = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(f => new FacilityModel
                {
                    Id = f.Id,
                    FacilityId = f.FacilityId,
                    FacilityName = f.FacilityName,
                    TimeZone = f.TimeZone,
                    IsDeleted = f.IsDeleted,
                    VendorVersionId = f.VendorVersion!.Id,
                    Vendor = new VendorModel()
                    {
                        Id = f.VendorVersion.Vendor!.Id,
                        Name = f.VendorVersion.Vendor.Name,
                        Authentication = f.VendorVersion.Vendor.Authentication
                    },
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
                var prop = type.GetProperty(inputSortBy,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
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