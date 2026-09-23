using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Normalization.Domain.Services;

namespace LantanaGroup.Link.Normalization.Domain.Managers;

public interface IVendorVersionOperationPresetManager
{
    Task<VendorVersionOperationPresetModel> Create(CreateVendorVersionOperationPresetModel model, CancellationToken cancellationToken = default);
    Task Delete(Guid vendorVersionId, Guid presetId, CancellationToken cancellationToken = default);
}

public class VendorVersionOperationPresetManager : IVendorVersionOperationPresetManager
{
    private readonly IDatabase _database;
    private readonly IOperationManager _operationManager;
    private readonly IVendorVersionOperationPresetQueries _presetQueries;
    private readonly IVendorVersionResolver _vendorVersionResolver;
    private readonly IOperationSequenceQueries _operationSequenceQueries;

    public VendorVersionOperationPresetManager(
        IDatabase database,
        IOperationManager operationManager,
        IVendorVersionOperationPresetQueries presetQueries,
        IVendorVersionResolver vendorVersionResolver,
        IOperationSequenceQueries operationSequenceQueries)
    {
        _database = database;
        _operationManager = operationManager;
        _presetQueries = presetQueries;
        _vendorVersionResolver = vendorVersionResolver;
        _operationSequenceQueries = operationSequenceQueries;
    }

    public async Task<VendorVersionOperationPresetModel> Create(CreateVendorVersionOperationPresetModel model, CancellationToken cancellationToken = default)
    {
        var operationResourceType = await _database.OperationResourceTypes.GetAsync(model.OperationResourceTypeId, cancellationToken);
        var operation = await _database.Operations.GetAsync(operationResourceType.OperationId, cancellationToken);
        if (operation.OperationType == OperationType.HSLOCMap.ToString())
        {
            throw new InvalidOperationException("HSLOC Map operations cannot be assigned to vendors.");
        }

        await _vendorVersionResolver.ResolveAsync([model.VendorVersionId], cancellationToken);

        var preset = await _database.VendorVersionOperationPresets.AddAsync(new VendorVersionOperationPreset
        {
            VendorVersionId = model.VendorVersionId,
            OperationResourceTypeId = model.OperationResourceTypeId,
            CreateDate = DateTime.UtcNow
        }, cancellationToken);

        await using var transaction = await _database.BeginTransactionAsync(cancellationToken);
        await _operationSequenceQueries.LockOperationAsync(operation.Id, cancellationToken);
        await _database.SaveChangesAsync(cancellationToken);
        await _operationSequenceQueries.InvalidateFacilitiesAsync(
            await _operationSequenceQueries.FacilitiesReferencingOperationAsync(operation.Id, cancellationToken),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (await _presetQueries.Get(preset.Id))!;
    }

    public async Task Delete(Guid vendorVersionId, Guid presetId, CancellationToken cancellationToken = default)
    {
        var preset = await _database.VendorVersionOperationPresets.SingleOrDefaultAsync(candidate =>
            candidate.Id == presetId && candidate.VendorVersionId == vendorVersionId, cancellationToken);

        if (preset == null)
        {
            return;
        }

        var operationResourceType = await _database.OperationResourceTypes.GetAsync(preset.OperationResourceTypeId, cancellationToken);
        await using var transaction = await _database.BeginTransactionAsync(cancellationToken);
        await _operationSequenceQueries.LockOperationAsync(operationResourceType.OperationId, cancellationToken);
        var operationPresets = await _database.VendorVersionOperationPresets.FindAsync(candidate =>
            candidate.OperationResourceTypeId == preset.OperationResourceTypeId, cancellationToken);
        if (operationPresets.All(candidate => candidate.Id != preset.Id))
        {
            return;
        }

        if (operationPresets.Count == 1)
        {
            await _operationManager.DeleteOperation(new DeleteOperationModel
            {
                OperationId = operationResourceType.OperationId,
                VendorVersionId = vendorVersionId
            }, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        _database.VendorVersionOperationPresets.Remove(preset);
        await _database.SaveChangesAsync(cancellationToken);
        await _operationSequenceQueries.InvalidateFacilitiesAsync(
            await _operationSequenceQueries.FacilitiesReferencingOperationAsync(operationResourceType.OperationId, cancellationToken),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}