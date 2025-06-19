using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace LantanaGroup.Link.Normalization.Domain.Queries
{
    public interface IOperationQueries
    {
        Task<OperationModel> Get(Guid id, string facilityId);
        Task<PagedConfigModel<OperationModel>> Search(OperationSearchModel model);
    }

    public class OperationQueries : IOperationQueries
    {
        private readonly IDatabase _database;
        private readonly NormalizationDbContext _dbContext;
        public OperationQueries(IDatabase database, NormalizationDbContext dbContext) 
        {
            _database = database;
            _dbContext = dbContext;
        }

        public async Task<OperationModel> Get(Guid id, string facilityId)
        {
            if(string.IsNullOrEmpty(facilityId))
            {
                throw new InvalidOperationException("FacilityID is required");
            }

            return (await Search(new OperationSearchModel()
            {
                OperationId = id,
                FacilityId = facilityId,
                IncludeDisabled = true
            })).Records.Single();
        }

        public async Task<PagedConfigModel<OperationModel>> Search(OperationSearchModel model)
        {
            var query = from o in _dbContext.Operations
                        where  model.FacilityId == null //No facility ID provided, bring back everyting (Admin Use Only)
                                    || (model.FacilityId != null && o.FacilityId == null && o.OperationResourceTypes.Any(ort => ort.OperationSequences.Any(os => os.FacilityId == model.FacilityId)))  //The caller wants a given facilities operations, so make sure to include vendor presets that are mapped
                                    || o.FacilityId == model.FacilityId // The Operation is for the provided facilityID
                        select new OperationModel()
                        {
                            Id = o.Id,
                            FacilityId = o.FacilityId,
                            Description = o.Description,
                            IsDisabled = o.IsDisabled,
                            ModifyDate = o.ModifyDate,
                            OperationJson = o.OperationJson,
                            OperationType = o.OperationType,
                            CreateDate = o.CreateDate,
                            Resources = o.OperationResourceTypes.Select(r => new ResourceModel()
                            {
                                ResourceName = r.ResourceType.Name,
                                ResourceTypeId = r.ResourceType.Id,
                            }).ToList(),
                            VendorPresets = o.OperationResourceTypes.SelectMany(r => r.VendorPresetOperationResourceTypes.Select(v => new VendorOperationPresetModel()
                            {
                                Id = v.VendorOperationPreset.Id,
                                Vendor = v.VendorOperationPreset.Vendor,
                                Description = v.VendorOperationPreset.Description,
                                Versions = v.VendorOperationPreset.Versions,
                                CreateDate = v.VendorOperationPreset.CreateDate,
                                ModifyDate = v.VendorOperationPreset.ModifyDate
                            })).ToList()
                        };

            if (model.OperationId.HasValue)
            {
                query = query.Where(q => q.Id == model.OperationId);
            }

            if (!string.IsNullOrEmpty(model.ResourceType))
            {
                query = query.Where(q => q.Resources.Any(r => r.ResourceName == model.ResourceType));
            }

            if (!model.IncludeDisabled)
            {
                query = query.Where(q => !q.IsDisabled);
            }

            if(model.OperationType.HasValue)
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
            
            return new PagedConfigModel<OperationModel>()
            {
                Records = records,
                Metadata = new PaginationMetadata(pageSize, pageNumber, count)
            };
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
