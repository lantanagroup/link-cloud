using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Services;
using Microsoft.EntityFrameworkCore;
using TenantVendorVersionModel = LantanaGroup.Link.Shared.Application.Models.Tenant.VendorVersionModel;

namespace LantanaGroup.Link.Normalization.Domain.Queries;

public interface IVendorVersionOperationPresetQueries
{
    Task<VendorVersionOperationPresetModel?> Get(Guid id);
    Task<List<VendorVersionOperationPresetModel>> Search(VendorVersionOperationPresetSearchModel model);
}

public class VendorVersionOperationPresetQueries : IVendorVersionOperationPresetQueries
{
    private readonly NormalizationDbContext _dbContext;
    private readonly IVendorVersionResolver _vendorVersionResolver;

    public VendorVersionOperationPresetQueries(NormalizationDbContext dbContext, IVendorVersionResolver vendorVersionResolver)
    {
        _dbContext = dbContext;
        _vendorVersionResolver = vendorVersionResolver;
    }

    public async Task<VendorVersionOperationPresetModel?> Get(Guid id)
    {
        return (await Search(new VendorVersionOperationPresetSearchModel { Id = id })).SingleOrDefault();
    }

    public async Task<List<VendorVersionOperationPresetModel>> Search(VendorVersionOperationPresetSearchModel model)
    {
        var presets = _dbContext.VendorVersionOperationPresets.AsQueryable();

        if (model.Id.HasValue)
        {
            presets = presets.Where(preset => preset.Id == model.Id.Value);
        }

        if (model.VendorVersionId.HasValue)
        {
            presets = presets.Where(preset => preset.VendorVersionId == model.VendorVersionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(model.Resource))
        {
            presets = presets.Where(preset => preset.OperationResourceType.ResourceType.Name == model.Resource);
        }

        var records = await presets.Select(preset => new VendorVersionOperationPresetModel
        {
            Id = preset.Id,
            VendorVersionId = preset.VendorVersionId,
            OperationResourceTypeId = preset.OperationResourceTypeId,
            OperationResourceType = new OperationResourceTypeModel
            {
                Id = preset.OperationResourceType.Id,
                OperationId = preset.OperationResourceType.OperationId,
                ResourceTypeId = preset.OperationResourceType.ResourceTypeId,
                Operation = new OperationModel
                {
                    Id = preset.OperationResourceType.Operation.Id,
                    Name = preset.OperationResourceType.Operation.Name,
                    Description = preset.OperationResourceType.Operation.Description,
                    OperationJson = preset.OperationResourceType.Operation.OperationJson,
                    OperationType = preset.OperationResourceType.Operation.OperationType
                },
                Resource = new ResourceModel
                {
                    ResourceName = preset.OperationResourceType.ResourceType.Name,
                    ResourceTypeId = preset.OperationResourceType.ResourceType.Id
                }
            },
            VendorVersion = new TenantVendorVersionModel { Id = preset.VendorVersionId },
            CreateDate = preset.CreateDate,
            ModifyDate = preset.ModifyDate
        }).ToListAsync();

        if (records.Count == 0)
        {
            return records;
        }

        var resolvedVendorVersions = await _vendorVersionResolver.ResolveAsync(records.Select(record => record.VendorVersionId));
        foreach (var record in records)
        {
            record.VendorVersion = resolvedVendorVersions[record.VendorVersionId];
        }

        return records;
    }
}