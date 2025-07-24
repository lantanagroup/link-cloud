using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Normalization.Domain.Queries
{
    public interface IVendorQueries
    {
        Task<VendorModel> GetVendor(Guid Id);
        Task<VendorModel?> GetVendor(string name);
        Task<List<VendorModel>> GetAllVendors();
        Task<VendorVersionModel> GetVendorVersion(Guid vendorId);
        Task<List<VendorModel>> SearchVendors(VendorSearchModel model);
        Task<VendorVersionOperationPresetModel> GetVendorVersionOperationPreset(Guid Id);
        Task<List<VendorVersionOperationPresetModel>> SearchVendorVersionOperationPreset(VendorOperationPresetSearchModel model);
    }

    public class VendorQueries : IVendorQueries
    {
        private readonly NormalizationDbContext _dbContext;
        public VendorQueries(NormalizationDbContext dbContext) 
        {
            _dbContext = dbContext;
        }

        public async Task<VendorModel> GetVendor(Guid Id)
        {
            return (await SearchVendors(new VendorSearchModel()
            {
                VendorId = Id,
            })).Single();
        }

        public async Task<List<VendorModel>> GetAllVendors()
        {
            return await SearchVendors(new VendorSearchModel());
        }

        public async Task<VendorModel?> GetVendor(string name)
        {
            return (await SearchVendors(new VendorSearchModel()
            {
                VendorName = name
            })).SingleOrDefault();
        }

        public async Task<VendorVersionModel> GetVendorVersion(Guid vendorId)
        {
            var query = from vv in _dbContext.VendorVersions
                              where vv.VendorId == vendorId
                              select new VendorVersionModel()
                              {
                                  Id = vv.Id,
                                  VendorId = vv.VendorId
                              };

            return await query.FirstAsync();
        }

        public async Task<VendorVersionOperationPresetModel> GetVendorVersionOperationPreset(Guid Id)
        {
            return (await SearchVendorVersionOperationPreset(new VendorOperationPresetSearchModel()
            {
                Id = Id,
            })).Single();
        }

        public async Task<List<VendorModel>> SearchVendors(VendorSearchModel model)
        {
            var query = from v in _dbContext.Vendors
                        select new VendorModel()
                        {
                            Id = v.Id,
                            Name = v.Name,
                            Versions = v.VendorVersions.Select(x => new VendorVersionModel()
                            {
                                Id = x.Id,
                                VendorId = x.VendorId,
                                Version = x.Version
                            }).ToList()
                        };

            if (!string.IsNullOrEmpty(model.VendorName))
            {
                query = query.Where(q => q.Name == model.VendorName);
            }

            if (model.VendorId.HasValue)
            {
                query = query.Where(q => q.Id == model.VendorId);
            }

            return await query.OrderBy(q => q.Name).ToListAsync();
        }

        public async Task<List<VendorVersionOperationPresetModel>> SearchVendorVersionOperationPreset(VendorOperationPresetSearchModel model)
        {
            var query = from o in _dbContext.VendorVersionOperationPresets
                        select new VendorVersionOperationPresetModel()
                        {
                            Id = o.Id,
                            VendorVersionId = o.VendorVersionId,
                            OperationResourceTypeId = o.OperationResourceTypeId,
                            OperationResourceType = o.OperationResourceType == null ? new() : new OperationResourceTypeModel()
                            {
                                Id = o.OperationResourceType.Id,
                                OperationId = o.OperationResourceType.OperationId,
                                ResourceTypeId = o.OperationResourceType.ResourceTypeId,
                                Operation = new OperationModel()
                                {
                                    Id = o.OperationResourceType.Operation.Id,
                                    Name = o.OperationResourceType.Operation.Name,
                                    Description = o.OperationResourceType.Operation.Description,
                                    OperationJson = o.OperationResourceType.Operation.OperationJson,
                                    OperationType = o.OperationResourceType.Operation.OperationType
                                },
                                Resource = new ResourceModel()
                                {
                                    ResourceName = o.OperationResourceType.ResourceType.Name,
                                    ResourceTypeId = o.OperationResourceType.ResourceType.Id
                                }
                            },
                            VendorVersion = new VendorVersionModel()
                            {
                                Id = o.VendorVersion.Id,
                                VendorId = o.VendorVersion.VendorId,
                                Version = o.VendorVersion.Version,
                                Vendor = new VendorModel()
                                {
                                    Id = o.VendorVersion.Vendor.Id,
                                    Name = o.VendorVersion.Vendor.Name
                                }
                            },
                            CreateDate = o.CreateDate,
                            ModifyDate = o.ModifyDate
                        };

            if (model.Id.HasValue)
            {
                query = query.Where(q => q.Id == model.Id);
            }

            if (model.VendorId != null)
            {
                query = query.Where(q => q.VendorVersion.Vendor.Id == model.VendorId);
            }

            if (model.Resource != null)
            {
                query = query.Where(q => q.OperationResourceType.Resource.ResourceName == model.Resource);
            }

            return await query.ToListAsync();
        }
    }
}
