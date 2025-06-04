using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Normalization.Domain.Queries
{
    public interface IOperationQueries
    {
        Task<OperationModel> Get(Guid Id, string? facilityId = null);
        Task<List<OperationModel>> Search(OperationSearchModel model);
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

        public async Task<OperationModel> Get(Guid Id, string? FacilityId = null)
        {
            return (await Search(new OperationSearchModel()
            {
                Id = Id,
                FacilityId = FacilityId,
            })).Single();
        }

        public async Task<List<OperationModel>> Search(OperationSearchModel model)
        {
            var query = from o in _dbContext.Operations
                        where o.FacilityId == model.FacilityId
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

            if (model.Id.HasValue)
            {
                query = query.Where(q => q.Id == model.Id);
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

            return await query.ToListAsync();
        }
    }
}
