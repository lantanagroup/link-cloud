using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Normalization.Domain.Queries
{
    public interface IOperationSequenceQueries
    {
        Task<OperationSequenceModel> Get(string resourceType, string? facilityId);
        Task<List<OperationSequenceModel>> Search(OperationSequenceSearchModel model);
    }

    public class OperationSequenceQueries : IOperationSequenceQueries
    {
        private readonly IDatabase _database;
        private readonly NormalizationDbContext _dbContext;
        public OperationSequenceQueries(IDatabase database, NormalizationDbContext dbContext) 
        {
            _database = database;
            _dbContext = dbContext;
        }

        public async Task<OperationSequenceModel> Get(string FacilityId, Guid id)
        {
            return (await Search(new OperationSequenceSearchModel()
            {
                ResourceTypeId = id,
                FacilityId = FacilityId,
            })).Single();
        }

        public async Task<OperationSequenceModel> Get(string FacilityId, string resourceType)
        {
            return (await Search(new OperationSequenceSearchModel()
            {
                ResourceType = resourceType,
                FacilityId = FacilityId,
            })).Single();
        }

        public async Task<List<OperationSequenceModel>> Search(OperationSequenceSearchModel model)
        {
            var query = from o in _dbContext.OperationSequences
                        where o.FacilityId == model.FacilityId
                        select new OperationSequenceModel()
                        {
                            Id = o.Id,
                            FacilityId = o.FacilityId,
                            Sequence = o.Sequence.HasValue ? o.Sequence.Value : default,
                            ModifyDate = o.ModifyDate,
                            CreateDate = o.CreateDate,      
                            OperationResourceType = new OperationResourceTypeModel()
                            {
                                Id = o.OperationResourceTypeId,
                                OperationId = o.OperationResourceType.OperationId,
                                ResourceTypeId = o.OperationResourceType.ResourceTypeId,
                                Operation = new OperationModel()
                                {
                                    Id = o.OperationResourceType.OperationId,
                                    FacilityId = o.OperationResourceType.Operation.FacilityId,
                                    Description = o.OperationResourceType.Operation.Description,
                                    IsDisabled = o.OperationResourceType.Operation.IsDisabled,
                                    ModifyDate = o.OperationResourceType.Operation.ModifyDate,
                                    OperationJson = o.OperationResourceType.Operation.OperationJson,
                                    OperationType = o.OperationResourceType.Operation.OperationType,
                                    CreateDate = o.OperationResourceType.Operation.CreateDate,
                                },
                                Resource = new ResourceModel()
                                {
                                    ResourceTypeId = o.OperationResourceType.ResourceType.Id,
                                    ResourceName = o.OperationResourceType.ResourceType.Name
                                }
                            },
                            VendorPresets = o.OperationResourceType.VendorVersionOperationPresets.Select(vp => new VendorVersionOperationPresetModel()
                            {
                                Id = vp.Id,
                                VendorVersionId = vp.VendorVersionId,
                                OperationResourceTypeId = vp.OperationResourceTypeId,
                                OperationResourceType = new OperationResourceTypeModel()
                                {
                                    Operation = new OperationModel()
                                    {
                                        Id = vp.OperationResourceType.Operation.Id,
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
                                VendorVersion = new VendorVersionModel()
                                {
                                    Id = vp.VendorVersion.Id,
                                    VendorId = vp.VendorVersion.VendorId,
                                    Version = vp.VendorVersion.Version,
                                    Vendor = new VendorModel()
                                    {
                                        Id = vp.VendorVersion.Vendor.Id,
                                        Name = vp.VendorVersion.Vendor.Name
                                    }
                                },
                                CreateDate = vp.CreateDate,
                                ModifyDate = vp.ModifyDate
                            }).ToList()
                        };

            if (!string.IsNullOrEmpty(model.ResourceType))
            {
                query = query.Where(q => q.OperationResourceType.Resource.ResourceName == model.ResourceType);
            }

            if (model.ResourceTypeId.HasValue)
            {
                query = query.Where(q => q.OperationResourceType.Resource.ResourceTypeId == model.ResourceTypeId);
            }

            return await query.ToListAsync();
        }
    }
}
