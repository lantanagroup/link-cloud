using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Normalization.Domain.Services;

namespace LantanaGroup.Link.Normalization.Domain.Managers;

public interface IVendorVersionOperationPresetManager
{
    Task<VendorVersionOperationPresetModel> Create(CreateVendorVersionOperationPresetModel model);
    Task Delete(Guid vendorVersionId, Guid presetId);
}

public class VendorVersionOperationPresetManager : IVendorVersionOperationPresetManager
{
    private readonly IDatabase _database;
    private readonly IOperationManager _operationManager;
    private readonly IVendorVersionOperationPresetQueries _presetQueries;
    private readonly IVendorVersionResolver _vendorVersionResolver;

    public VendorVersionOperationPresetManager(
        IDatabase database,
        IOperationManager operationManager,
        IVendorVersionOperationPresetQueries presetQueries,
        IVendorVersionResolver vendorVersionResolver)
    {
        _database = database;
        _operationManager = operationManager;
        _presetQueries = presetQueries;
        _vendorVersionResolver = vendorVersionResolver;
    }

    public async Task<VendorVersionOperationPresetModel> Create(CreateVendorVersionOperationPresetModel model)
    {
        await _vendorVersionResolver.ResolveAsync([model.VendorVersionId]);

        var preset = await _database.VendorVersionOperationPresets.AddAsync(new VendorVersionOperationPreset
        {
            VendorVersionId = model.VendorVersionId,
            OperationResourceTypeId = model.OperationResourceTypeId,
            CreateDate = DateTime.UtcNow
        });

        await _database.SaveChangesAsync();

        return (await _presetQueries.Get(preset.Id))!;
    }

    public async Task Delete(Guid vendorVersionId, Guid presetId)
    {
        var preset = await _database.VendorVersionOperationPresets.SingleOrDefaultAsync(candidate =>
            candidate.Id == presetId && candidate.VendorVersionId == vendorVersionId);

        if (preset == null)
        {
            return;
        }

        var operationResourceType = await _database.OperationResourceTypes.GetAsync(preset.OperationResourceTypeId);
        var operationPresets = await _database.VendorVersionOperationPresets.FindAsync(candidate =>
            candidate.OperationResourceTypeId == preset.OperationResourceTypeId);

        if (operationPresets.Count == 1)
        {
            await _operationManager.DeleteOperation(new DeleteOperationModel
            {
                OperationId = operationResourceType.OperationId,
                VendorVersionId = vendorVersionId
            });
            return;
        }

        _database.VendorVersionOperationPresets.Remove(preset);
        await _database.SaveChangesAsync();
    }
}