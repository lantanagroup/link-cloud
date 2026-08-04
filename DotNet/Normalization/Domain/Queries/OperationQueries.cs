using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Services;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using TenantVendorVersionModel = LantanaGroup.Link.Shared.Application.Models.Tenant.VendorVersionModel;

namespace LantanaGroup.Link.Normalization.Domain.Queries
{
    public interface IOperationQueries
    {
        Task<OperationModel> Get(Guid id, string? facilityId = null);
        Task<PagedConfigModel<OperationModel>> Search(OperationSearchModel model);
    }

    public class OperationQueries : IOperationQueries
    {
        private readonly IDatabase _database;
        private readonly NormalizationDbContext _dbContext;
        private readonly IVendorVersionResolver _vendorVersionResolver;

        public OperationQueries(IDatabase database, NormalizationDbContext dbContext, IVendorVersionResolver vendorVersionResolver)
        {
            _database = database;
            _dbContext = dbContext;
            _vendorVersionResolver = vendorVersionResolver;
        }

        public async Task<OperationModel> Get(Guid id, string? facilityId = null)
        {
            return (await Search(new OperationSearchModel()
            {
                OperationId = id,
                FacilityId = facilityId,
                IncludeDisabled = true
            })).Records.FirstOrDefault();
        }

        public async Task<PagedConfigModel<OperationModel>> Search(OperationSearchModel model)
        {
            var query = from o in _dbContext.Operations
                        select new OperationModel()
                        {
                            Id = o.Id,
                            FacilityId = o.FacilityId,
                            Name = o.Name,
                            Description = o.Description,
                            IsDisabled = o.IsDisabled,
                            ModifyDate = o.ModifyDate,
                            OperationJson = o.OperationJson,
                            OperationType = o.OperationType,
                            CreateDate = o.CreateDate,
                            OperationResourceTypes = o.OperationResourceTypes.Select(ort => new OperationResourceTypeModel()
                            {
                                Id = ort.Id,
                                OperationId = ort.OperationId,
                                ResourceTypeId = ort.ResourceTypeId,
                                Resource = new ResourceModel()
                                {
                                    ResourceName = ort.ResourceType.Name,
                                    ResourceTypeId = ort.ResourceType.Id,
                                }
                            }).ToList(),
                            VendorPresets = o.OperationResourceTypes.SelectMany(r => r.VendorVersionOperationPresets.Select(vp => new VendorVersionOperationPresetModel()
                            {
                                Id = vp.Id,
                                VendorVersionId = vp.VendorVersionId,
                                OperationResourceTypeId = vp.OperationResourceTypeId,
                                OperationResourceType = new OperationResourceTypeModel()
                                {
                                    Id = vp.OperationResourceType.Id,
                                    OperationId = vp.OperationResourceType.OperationId,
                                    ResourceTypeId = vp.OperationResourceType.ResourceTypeId,
                                    Operation = new OperationModel()
                                    {
                                        Id = vp.OperationResourceType.Operation.Id,
                                        Name = vp.OperationResourceType.Operation.Name,
                                        Description = vp.OperationResourceType.Operation.Description,
                                        OperationJson = vp.OperationResourceType.Operation.OperationJson,
                                        OperationType = vp.OperationResourceType.Operation.OperationType
                                    },
                                    Resource = new ResourceModel()
                                    {
                                        ResourceName = vp.OperationResourceType.ResourceType.Name,
                                        ResourceTypeId = vp.OperationResourceType.ResourceType.Id
                                    }
                                },
                                VendorVersion = new TenantVendorVersionModel()
                                {
                                    Id = vp.VendorVersionId
                                },
                                CreateDate = vp.CreateDate,
                                ModifyDate = vp.ModifyDate
                            })).ToList()
                        };

            if (!string.IsNullOrEmpty(model.FacilityId) && model.VendorVersionId != null)
            {
                query = query.Where(o => o.FacilityId == model.FacilityId || o.VendorPresets.Any(vp => vp.VendorVersionId == model.VendorVersionId));
            }
            else if (!string.IsNullOrEmpty(model.FacilityId))
            {
                query = query.Where(o => o.FacilityId == model.FacilityId);
            }
            else if (model.VendorVersionId.HasValue)
            {
                query = query.Where(o => o.VendorPresets.Any(vp => vp.VendorVersionId == model.VendorVersionId));
            }

            if (model.OperationId.HasValue)
            {
                query = query.Where(q => q.Id == model.OperationId);
            }

            if (!string.IsNullOrEmpty(model.ResourceType))
            {
                query = query.Where(q => q.OperationResourceTypes.Any(r => r.Resource.ResourceName == model.ResourceType));
            }

            if (!model.IncludeDisabled)
            {
                query = query.Where(q => !q.IsDisabled);
            }

            if (model.OperationType.HasValue)
            {
                var opType = model.OperationType.ToString();
                query = query.Where(q => q.OperationType == opType);
            }

            var sortOrder = model.SortOrder ?? SortOrder.Descending;
            var sortBy = model.SortBy ?? "Id";

            query = sortOrder switch
            {
                SortOrder.Ascending => query.OrderBy(SetSortBy<OperationModel>(sortBy)),
                SortOrder.Descending => query.OrderByDescending(SetSortBy<OperationModel>(sortBy)),
                _ => query
            };

            var pageNumber = model.PageNumber ?? 1;
            var pageSize = model.PageSize ?? 10;

            var count = await query.CountAsync();

            var records = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

            await HydrateVendorVersionsAsync(records);

            return new PagedConfigModel<OperationModel>()
            {
                Records = records,
                Metadata = new PaginationMetadata(pageSize, pageNumber, count)
            };
        }

        private async Task HydrateVendorVersionsAsync(IEnumerable<OperationModel> operations)
        {
            var presets = operations.SelectMany(operation => operation.VendorPresets).ToList();
            if (presets.Count == 0)
            {
                return;
            }

            var resolvedVendorVersions = await _vendorVersionResolver.ResolveAsync(presets.Select(preset => preset.VendorVersionId));
            foreach (var preset in presets)
            {
                preset.VendorVersion = resolvedVendorVersions[preset.VendorVersionId];
            }
        }

        private Expression<Func<T, object>> SetSortBy<T>(string? sortBy)
        {
            var sortKey = sortBy?.ToLower() ?? "";
            var parameter = Expression.Parameter(typeof(T), "p");
            var sortExpression = Expression.Lambda<Func<T, object>>(Expression.Convert(Expression.Property(parameter, sortKey), typeof(object)), parameter);

            return sortExpression;
        }
    }
}
