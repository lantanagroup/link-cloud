using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Business.Models;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace LantanaGroup.Link.Tenant.Business.Queries
{
    public interface IFacilityQueries
    {
        Task<FacilityModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);
        Task<FacilityModel?> GetAsync(string facilityId, string? facilityName = null, CancellationToken cancellationToken = default);
        Task<List<FacilityModel>> SearchAsync(FacilitySearchModel model, CancellationToken cancellationToken = default);
        Task<PagedConfigModel<FacilityModel>> SearchAsync(FacilitySearchModel searchModel, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default);
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
            if (string.IsNullOrEmpty(model.FacilityId) && model.Id == null && string.IsNullOrEmpty(model.FacilityName))
            {
                throw new InvalidOperationException("Either the FacilityId, Id, FacilityName, or a combination must be populated.");
            }

            var query = _context.Facilities.AsQueryable();

            if (model.Id.HasValue)
            {
                query = query.Where(f => f.Id == model.Id.Value);
            }

            if (!string.IsNullOrEmpty(model.FacilityId))
            {
                query = query.Where(f => f.FacilityId == model.FacilityId);
            }

            if (!string.IsNullOrEmpty(model.FacilityName))
            {
                if (model.FacilityNameContains ?? false)
                {
                    query = query.Where(f => f.FacilityName.ToLower().Contains(model.FacilityName.ToLower()));
                }
                else
                {
                    query = query.Where(f => f.FacilityName == model.FacilityName);
                }
            }

            return await query.Select(f => new FacilityModel()
            {
                Id = f.Id,
                FacilityName = f.FacilityName,
                FacilityId = f.FacilityId,
                TimeZone = f.TimeZone,
                ScheduledReports = new TenantScheduledReportConfig()
                {
                    Daily = f.ScheduledReports.Daily,
                    Weekly = f.ScheduledReports.Weekly,
                    Monthly = f.ScheduledReports.Monthly
                }
            }).ToListAsync(cancellationToken);
        }

        public async Task<PagedConfigModel<FacilityModel>> SearchAsync(FacilitySearchModel searchModel, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default)
        {
            Expression<Func<Facility, bool>>? predicate = null;

            if (searchModel.Id.HasValue || !string.IsNullOrEmpty(searchModel.FacilityId) || !string.IsNullOrEmpty(searchModel.FacilityName))
            {
                predicate = f =>
                    (!searchModel.Id.HasValue || f.Id == searchModel.Id.Value) &&
                    (string.IsNullOrEmpty(searchModel.FacilityId) || f.FacilityId == searchModel.FacilityId) &&
                    (string.IsNullOrEmpty(searchModel.FacilityName) ||
                        ((searchModel.FacilityNameContains ?? false) ?
                            f.FacilityName.ToLower().Contains(searchModel.FacilityName.ToLower()) :
                            f.FacilityName == searchModel.FacilityName));
            }

            var (entities, metadata) = await _entityRepository.SearchAsync(predicate, sortBy, sortOrder, pageSize, pageNumber, cancellationToken);

            var models = entities.Select(f => new FacilityModel
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
                }
            }).ToList();

            return new PagedConfigModel<FacilityModel> { Records = models, Metadata = metadata };
        }
    }
}
