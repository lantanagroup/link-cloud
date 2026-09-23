﻿using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Normalization.Domain.Services;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Shared.Application.Enums;

namespace LantanaGroup.Link.Normalization.Domain.Managers
{
    public class TaskResult
    {
        public bool IsSuccess { get; set; }
        public object? ObjectResult { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public interface IOperationManager
    {
        Task<TaskResult> CreateOperation(CreateOperationModel model, CancellationToken cancellationToken = default);
        Task<TaskResult> UpdateOperation(UpdateOperationModel model, CancellationToken cancellationToken = default);
        Task<bool> DeleteOperation(DeleteOperationModel deleteOperationModel, CancellationToken cancellationToken = default);
        Task UpdateVendorPresetsForOperation(Guid operationId, List<Guid>? vendorVersionIds, CancellationToken cancellationToken = default);
        Task UpdateOperationResourceTypesForOperation(Guid operationId, List<ResourceModel> resources, CancellationToken cancellationToken = default);
        Task UpdateOperationResourceTypesForOperation(Guid operationId, List<string> resourceTypes, CancellationToken cancellationToken = default);
        Task<List<OperationSequenceModel>> CreateOperationSequences(CreateOperationSequencesModel model, CancellationToken cancellationToken = default);
        Task<List<OperationSequenceModel>> AppendOperationToSequence(string facilityId, string resourceType, Guid operationId, CancellationToken cancellationToken = default);
        Task<bool> DeleteOperationSequence(DeleteOperationSequencesModel deleteOperationSequencesModel, CancellationToken cancellationToken = default);
    }

    public class OperationManager : IOperationManager
    {
        private readonly IDatabase _database;
        private readonly IResourceManager _resourceManager;
        private readonly IOperationQueries _operationQueries;
        private readonly IOperationSequenceQueries _operationSequenceQueries;
        private readonly IResourceQueries _resourceQueries;
        private readonly IVendorVersionResolver _vendorVersionResolver;
        private readonly IHSLOCQueries _hslocQueries;

        public OperationManager(IDatabase database, IOperationQueries operationQueries, IOperationSequenceQueries operationSequenceQueries, IResourceQueries resourceQueries, IResourceManager resourceManager, IVendorVersionResolver vendorVersionResolver, IHSLOCQueries hslocQueries)
        {
            _database = database;
            _operationQueries = operationQueries;
            _operationSequenceQueries = operationSequenceQueries;
            _resourceQueries = resourceQueries;
            _resourceManager = resourceManager;
            _vendorVersionResolver = vendorVersionResolver;
            _hslocQueries = hslocQueries;
        }

        public async Task<TaskResult> CreateOperation(CreateOperationModel model, CancellationToken cancellationToken = default)
        {
            TaskResult taskResult = new();
            try
            {
                if (string.IsNullOrEmpty(model.FacilityId) && (!model.VendorVersionIds?.Any() ?? true))
                {
                    throw new Exception("An operation must either be configured with a FacilityID or one or more Vendor Version IDs.");
                }

                if (!string.IsNullOrEmpty(model.FacilityId) && (model.VendorVersionIds?.Any() ?? false))
                {
                    throw new Exception("An operation must either be configured with a FacilityID or one or more Vendor Version IDs, but not both.");
                }

                if (model.OperationType == "HSLOCMap" && !string.IsNullOrEmpty(model.FacilityId) &&
                    await _database.Operations.AnyAsync(operation => operation.FacilityId == model.FacilityId && operation.OperationType == "HSLOCMap", cancellationToken))
                {
                    taskResult.IsSuccess = false;
                    taskResult.ObjectResult = null;
                    taskResult.ErrorMessage = "Only one HSLOC Map operation is allowed per facility.";
                    return taskResult;
                }

                var result = await ValidateOperation(model.OperationType, model.OperationJson, model.ResourceTypes, cancellationToken);

                if (!result.IsValid)
                {
                    taskResult.IsSuccess = false;
                    taskResult.ObjectResult = null;
                    taskResult.ErrorMessage = result.ErrorMessage;

                    return taskResult;
                }

                var operation = await ExecuteWithDeadlockRetryAsync(async () =>
                {
                    var created = new Operation()
                    {
                        OperationType = model.OperationType,
                        OperationJson = model.OperationJson,
                        FacilityId = model.FacilityId,
                        Name = model.Name,
                        Description = model.Description,
                        IsDisabled = model.IsDisabled,
                        CreateDate = DateTime.UtcNow,
                        ModifyDate = null
                    };

                    await using var transaction = await _database.BeginTransactionAsync(cancellationToken);
                    await _database.Operations.AddAsync(created, cancellationToken);
                    await _database.SaveChangesAsync(cancellationToken);

                    await UpdateOperationResourceTypesForOperation(created.Id, model.ResourceTypes, cancellationToken);
                    await UpdateVendorPresetsForOperation(created.Id, model.VendorVersionIds, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return created;
                }, cancellationToken);

                taskResult.IsSuccess = true;
                taskResult.ObjectResult = await ExecuteWithDeadlockRetryAsync(
                    () => _operationQueries.Get(operation.Id, operation.FacilityId, cancellationToken),
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                taskResult.IsSuccess = false;
                taskResult.ErrorMessage = ex.Message + Environment.NewLine + ex.StackTrace;
            }

            return taskResult;
        }

        public async Task<TaskResult> UpdateOperation(UpdateOperationModel model, CancellationToken cancellationToken = default)
        {
            TaskResult taskResult = new();
            try
            {
                if (string.IsNullOrEmpty(model.FacilityId) && (!model.VendorVersionIds?.Any() ?? true))
                {
                    throw new Exception("An operation must either be configured with a FacilityID or one or more Vendor Version IDs.");
                }

                if (!string.IsNullOrEmpty(model.FacilityId) && (model.VendorVersionIds?.Any() ?? false))
                {
                    throw new Exception("An operation must either be configured with a FacilityID or one or more Vendor Version IDs, but not both.");
                }

                await using var transaction = await _database.BeginTransactionAsync(cancellationToken);
                if (model.ResourceTypes != null)
                {
                    foreach (var resourceName in model.ResourceTypes.Where(name => !string.IsNullOrEmpty(name)).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal))
                    {
                        await _operationSequenceQueries.LockResourceTypeAsync(resourceName, cancellationToken);
                    }
                }

                await _operationSequenceQueries.LockOperationAsync(model.Id, cancellationToken);

                var operation = await _database.Operations.GetAsync(model.Id, cancellationToken);
                if (operation == null)
                {
                    throw new InvalidOperationException($"No Operation Found for Id {model.Id}");
                }

                var hasVendorPresets = await _database.VendorVersionOperationPresets.AnyAsync(
                    preset => preset.OperationResourceType.OperationId == model.Id,
                    cancellationToken);
                if (!string.IsNullOrEmpty(operation.FacilityId) && (model.VendorVersionIds?.Any() ?? false))
                {
                    throw new InvalidOperationException("The operation for the provided Id is a facility operation, but the update model has provided vendor version IDs. A facility operation cannot also be a vendor operation or vice versa. Create a new Operation for the facility or vendor version(s).");
                }

                if (hasVendorPresets && !string.IsNullOrEmpty(model.FacilityId))
                {
                    throw new InvalidOperationException("The operation for the provided Id is a vendor operation, but the update model has provided a FacilityId. A facility operation cannot also be a vendor operation or vice versa. Create a new Operation for the facility or vendor(s).");
                }
                if (operation.OperationType == "HSLOCMap" && !string.IsNullOrEmpty(model.FacilityId) &&
                    await _database.Operations.AnyAsync(existing => existing.FacilityId == model.FacilityId && existing.OperationType == "HSLOCMap" && existing.Id != model.Id, cancellationToken))
                {
                    taskResult.IsSuccess = false;
                    taskResult.ObjectResult = null;
                    taskResult.ErrorMessage = "Only one HSLOC Map operation is allowed per facility.";
                    return taskResult;
                }

                operation.OperationResourceTypes = await _database.OperationResourceTypes.FindAsync(m => m.OperationId == model.Id, cancellationToken);

                var result = await ValidateOperation(operation.OperationType.ToString(), model.OperationJson, model.ResourceTypes, cancellationToken);

                if (!result.IsValid)
                {
                    taskResult.IsSuccess = false;
                    taskResult.ObjectResult = null;
                    taskResult.ErrorMessage = result.ErrorMessage;

                    return taskResult;
                }

                var previousFacilityId = operation.FacilityId;
                operation.FacilityId = model.FacilityId;
                operation.Name = model.Name;
                operation.Description = model.Description;
                operation.OperationJson = model.OperationJson;
                operation.IsDisabled = model.IsDisabled;
                operation.ModifyDate = DateTime.UtcNow;

                var affectedFacilities = await _operationSequenceQueries.FacilitiesReferencingOperationAsync(model.Id, cancellationToken);
                if (!string.IsNullOrEmpty(model.FacilityId))
                {
                    affectedFacilities.Add(model.FacilityId);
                }

                if (!string.IsNullOrEmpty(previousFacilityId))
                {
                    affectedFacilities.Add(previousFacilityId);
                }

                await _database.SaveChangesAsync(cancellationToken);
                await UpdateOperationResourceTypesForOperation(model.Id, model.ResourceTypes, cancellationToken);
                await UpdateVendorPresetsForOperation(model.Id, model.VendorVersionIds, cancellationToken);
                await _operationSequenceQueries.InvalidateFacilitiesAsync(affectedFacilities, cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                taskResult.IsSuccess = true;
                taskResult.ObjectResult = await ExecuteWithDeadlockRetryAsync(
                    () => _operationQueries.Get(operation.Id, operation.FacilityId, cancellationToken),
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                taskResult.IsSuccess = false;
                taskResult.ErrorMessage = ex.Message;
            }

            return taskResult;
        }

        private async Task<(bool IsValid, string? ErrorMessage)> ValidateOperation(
            string operationType, string operationJson, List<string> resourceTypes, CancellationToken cancellationToken)
        {
            var result = await OperationServiceHelper.ValidateOperation(operationType, operationJson, resourceTypes);
            if (!result.IsValid || operationType != nameof(OperationType.HSLOCMap))
                return result;

            //additional validation for HSLOCMap operations: all target codes must match active HSLOC codes
            var operation = OperationHelper.GetOperation(operationType, operationJson) as HSLOCMapOperation;
            if(operation == null) return result;
            
            var targetCodes = operation.CodeSystemMaps.SelectMany(map => map.CodeMaps.Values)
                .Select(map => map.Code).Distinct(StringComparer.Ordinal).ToList();
            if (targetCodes.Count == 0)
                return result;

            var activeCodes = (await _hslocQueries.GetAll(false, cancellationToken))
                .Select(row => row.HSLOCCode).ToHashSet(StringComparer.Ordinal);
            var invalidCodes = targetCodes.Where(code => !activeCodes.Contains(code)).ToList();
            return invalidCodes.Count == 0
                ? result
                : (false, $"HSLOCMap target codes must match active HSLOC codes. Invalid codes: {string.Join(", ", invalidCodes)}.");
        }

        public async Task UpdateVendorPresetsForOperation(Guid operationId, List<Guid>? vendorVersionIds, CancellationToken cancellationToken = default)
        {
            if (vendorVersionIds == null)
            {
                return;
            }

            var resolvedVendorVersions = await _vendorVersionResolver.ResolveAsync(vendorVersionIds, cancellationToken);
            var ownsTransaction = !_database.HasActiveTransaction;
            var transaction = ownsTransaction ? await _database.BeginTransactionAsync(cancellationToken) : null;
            try
            {
                await _operationSequenceQueries.LockOperationAsync(operationId, cancellationToken);
                await SaveVendorPresetsAsync(operationId, vendorVersionIds, resolvedVendorVersions.Keys, cancellationToken);
                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }

        private async Task SaveVendorPresetsAsync(Guid operationId, List<Guid> vendorVersionIds, IEnumerable<Guid> resolvedVendorVersionIds, CancellationToken cancellationToken)
        {
            var orts = (await _database.OperationResourceTypes.FindAsync(m => m.OperationId == operationId, cancellationToken)).Select(ort => ort.Id);

            foreach (var ort in orts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var toDelete = await _database.VendorVersionOperationPresets.FindAsync(vp => vp.OperationResourceTypeId == ort && !vendorVersionIds.Contains(vp.VendorVersionId), cancellationToken);

                foreach (var delete in toDelete)
                {
                    _database.VendorVersionOperationPresets.Remove(delete);
                }

                foreach (var vendorVersionId in resolvedVendorVersionIds)
                {
                    if (!await _database.VendorVersionOperationPresets.AnyAsync(vop => vop.VendorVersionId == vendorVersionId && vop.OperationResourceTypeId == ort, cancellationToken))
                    {
                        await _database.VendorVersionOperationPresets.AddAsync(new VendorVersionOperationPreset()
                        {
                            OperationResourceTypeId = ort,
                            VendorVersionId = vendorVersionId
                        }, cancellationToken);
                    }
                }
            }

            await _database.SaveChangesAsync(cancellationToken);
            await InvalidateCachedSequencesAsync(operationId, cancellationToken);
        }

        public async Task UpdateOperationResourceTypesForOperation(Guid operationId, List<string> resourceTypes, CancellationToken cancellationToken = default)
        {
            if (resourceTypes == null || resourceTypes.Count == 0)
            {
                throw new InvalidOperationException("ResourceTypes must be provided.");
            }

            List<ResourceModel> resources = new List<ResourceModel>();
            foreach (var res in resourceTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(res)) continue;

                var resource = await _resourceQueries.Get(res, cancellationToken);

                if (resource == null)
                {
                    resource = await _resourceManager.CreateResource(res, cancellationToken: cancellationToken);
                }

                resources.Add(resource);
            }

            if (resources.Count != resourceTypes.Where(r => !string.IsNullOrEmpty(r)).Count())
            {
                throw new InvalidOperationException("Not all provided Resource Types were found.");
            }

            await UpdateOperationResourceTypesForOperation(operationId, resources, cancellationToken);
        }

        public async Task UpdateOperationResourceTypesForOperation(Guid operationId, List<ResourceModel> resources, CancellationToken cancellationToken = default)
        {
            var ownsTransaction = !_database.HasActiveTransaction;
            var transaction = ownsTransaction ? await _database.BeginTransactionAsync(cancellationToken) : null;
            try
            {
            if (ownsTransaction)
            {
                // CreateOperationSequences locks this operation before it inserts a sequence.
                // Hold that lock before snapshotting sequences so a concurrent create cannot
                // insert one and bump a facility that this delete then cascades away.
                await _operationSequenceQueries.LockOperationAsync(operationId, cancellationToken);
            }

            var operation = await _database.Operations.GetAsync(operationId, cancellationToken);
            operation.OperationResourceTypes = await _database.OperationResourceTypes.FindAsync(m => m.OperationId == operationId, cancellationToken);
            var affectedFacilities = new HashSet<string>(StringComparer.Ordinal);

            //Delete any OperationResourceTypes that exist in the DB but not on the incoming model
            foreach (var ort in operation.OperationResourceTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!resources.Any(r => r.ResourceTypeId == ort.ResourceTypeId))
                {
                    var sequences = await _database.OperationSequences.FindAsync(os => os.OperationResourceTypeId == ort.Id, cancellationToken);
                    foreach (var sequence in sequences)
                    {
                        if (!string.IsNullOrEmpty(sequence.FacilityId))
                        {
                            affectedFacilities.Add(sequence.FacilityId);
                        }
                    }

                    sequences.ForEach(_database.OperationSequences.Remove);

                    var vops = await _database.VendorVersionOperationPresets.FindAsync(vop => vop.OperationResourceTypeId == ort.Id, cancellationToken);
                    vops.ForEach(_database.VendorVersionOperationPresets.Remove);

                    _database.OperationResourceTypes.Remove(ort);
                }
            }

            //Create any OperationResourceTypes that exist on the incoming model but not in the DB
            foreach (var resource in resources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!operation.OperationResourceTypes.Any(ort => ort.ResourceTypeId == resource.ResourceTypeId && ort.Operation.Id == operation.Id))
                {
                    var ort = new OperationResourceType()
                    {
                        OperationId = operation.Id,
                        ResourceTypeId = resource.ResourceTypeId
                    };

                    operation.OperationResourceTypes.Add(ort);
                    await _database.OperationResourceTypes.AddAsync(ort, cancellationToken);
                }
            }

            await _database.SaveChangesAsync(cancellationToken);
            if (affectedFacilities.Count > 0)
            {
                await _operationSequenceQueries.InvalidateFacilitiesAsync(affectedFacilities, cancellationToken);
            }

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }

        private async Task InvalidateCachedSequencesAsync(Guid operationId, CancellationToken cancellationToken, params string?[] additionalFacilityIds)
        {
            await _operationSequenceQueries.LockOperationAsync(operationId, cancellationToken);
            var facilityIds = await _operationSequenceQueries.FacilitiesReferencingOperationAsync(operationId, cancellationToken);
            facilityIds.AddRange(additionalFacilityIds.Where(id => !string.IsNullOrEmpty(id))!);
            await _operationSequenceQueries.InvalidateFacilitiesAsync(facilityIds, cancellationToken);
        }

        public async Task<bool> DeleteOperation(DeleteOperationModel model, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(model.FacilityId) && model.VendorVersionId == null)
            {
                throw new InvalidOperationException("Request must include a valid facilityId or vendor");
            }

            var ownsTransaction = !_database.HasActiveTransaction;
            var transaction = ownsTransaction ? await _database.BeginTransactionAsync(cancellationToken) : null;
            var modifiedRecords = 0;
            var affectedFacilities = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(model.FacilityId))
            {
                affectedFacilities.Add(model.FacilityId);
            }

            try
            {
                if (!string.IsNullOrEmpty(model.FacilityId))
                {
                    await LockResourceTypesBeforeFacilityAsync(model.FacilityId, model.ResourceType, cancellationToken);
                    await _operationSequenceQueries.LockFacilitySequenceWritesAsync(model.FacilityId, cancellationToken);
                }

                var operationIds = new List<Guid>();
                var pageNumber = 1;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var operations = await _operationQueries.Search(new OperationSearchModel()
                    {
                        FacilityId = model.FacilityId,
                        VendorVersionId = model.VendorVersionId,
                        OperationId = model.OperationId,
                        ResourceType = model.ResourceType,
                        IncludeDisabled = true,
                        SortBy = "Id",
                        SortOrder = SortOrder.Ascending,
                        PageNumber = pageNumber
                    }, cancellationToken, hydrateVendors: false);

                    if (operations == null || operations.Records.Count == 0)
                    {
                        break;
                    }

                    operationIds.AddRange(operations.Records.Select(record => record.Id));
                    if (pageNumber >= operations.Metadata.TotalPages)
                    {
                        break;
                    }

                    pageNumber++;
                }

                // SQL Server uniqueidentifier order is not .NET Guid order. Sequence writes sort
                // every id with GuidComparer, so the delete must lock in that same order.
                foreach (var operationId in operationIds.Distinct().OrderBy(id => id))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await _operationSequenceQueries.LockOperationAsync(operationId, cancellationToken);
                    var current = await _database.Operations.GetAsync(operationId, cancellationToken);
                    if (current == null || !await OperationStillMatchesDeleteAsync(current, model, cancellationToken))
                    {
                        continue;
                    }

                    modifiedRecords++;
                    foreach (var facilityId in await _operationSequenceQueries.FacilitiesReferencingOperationAsync(operationId, cancellationToken))
                    {
                        affectedFacilities.Add(facilityId);
                    }

                    if (!string.IsNullOrEmpty(model.FacilityId))
                    {
                        await DeleteOperationSequence(new DeleteOperationSequencesModel()
                        {
                            FacilityId = model.FacilityId,
                            OperationId = operationId,
                        }, cancellationToken);
                    }

                    var orts = await _database.OperationResourceTypes.FindAsync(ort => ort.OperationId == operationId, cancellationToken);
                    orts.ForEach(_database.OperationResourceTypes.Remove);

                    var vops = await _database.VendorVersionOperationPresets.FindAsync(vop => vop.OperationResourceType.OperationId == operationId, cancellationToken);
                    vops.ForEach(_database.VendorVersionOperationPresets.Remove);

                    var op = await _database.Operations.GetAsync(operationId, cancellationToken);
                    _database.Operations.Remove(op);
                }

                if (modifiedRecords > 0)
                {
                    await _database.SaveChangesAsync(cancellationToken);
                    await _operationSequenceQueries.InvalidateFacilitiesAsync(affectedFacilities, cancellationToken);
                    if (transaction != null)
                    {
                        await transaction.CommitAsync(cancellationToken);
                    }

                    return true;
                }

                return false;
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }

        private async Task LockResourceTypesBeforeFacilityAsync(string facilityId, string? resourceType, CancellationToken cancellationToken)
        {
            var names = string.IsNullOrEmpty(resourceType)
                ? await _operationSequenceQueries.ResourceTypeNamesForFacilityAsync(facilityId, cancellationToken)
                : new List<string> { resourceType };

            foreach (var name in names.Where(name => !string.IsNullOrEmpty(name)).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal))
            {
                await _operationSequenceQueries.LockResourceTypeAsync(name, cancellationToken);
            }
        }

        private async Task<bool> OperationStillMatchesDeleteAsync(Operation operation, DeleteOperationModel model, CancellationToken cancellationToken)
        {
            if (model.OperationId.HasValue && operation.Id != model.OperationId.Value)
            {
                return false;
            }

            var facilitySpecified = !string.IsNullOrEmpty(model.FacilityId);
            var vendorSpecified = model.VendorVersionId != null;
            var facilityMatches = facilitySpecified && operation.FacilityId == model.FacilityId;
            var vendorMatches = vendorSpecified && await _database.VendorVersionOperationPresets.AnyAsync(
                preset => preset.VendorVersionId == model.VendorVersionId && preset.OperationResourceType.OperationId == operation.Id,
                cancellationToken);
            var scopeMatches = facilitySpecified && vendorSpecified
                ? facilityMatches || vendorMatches
                : (!facilitySpecified || facilityMatches) && (!vendorSpecified || vendorMatches);
            if (!scopeMatches)
            {
                return false;
            }

            if (string.IsNullOrEmpty(model.ResourceType))
            {
                return true;
            }

            return await _database.OperationResourceTypes.AnyAsync(
                ort => ort.OperationId == operation.Id && ort.ResourceType.Name == model.ResourceType,
                cancellationToken);
        }

        public async Task<List<OperationSequenceModel>> AppendOperationToSequence(string facilityId, string resourceType, Guid operationId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(facilityId))
            {
                throw new InvalidOperationException("No FacilityId Provided");
            }

            if (string.IsNullOrEmpty(resourceType))
            {
                throw new InvalidOperationException("No Resource Found.");
            }

            return await ExecuteWithDeadlockRetryAsync(async () =>
            {
            await using var transaction = await _database.BeginTransactionAsync(cancellationToken);
            await _operationSequenceQueries.LockResourceTypeAsync(resourceType, cancellationToken);
            await _operationSequenceQueries.LockFacilitySequenceWritesAsync(facilityId, cancellationToken);

            var existingOperationIds = await _operationSequenceQueries.OperationsInFacilitySequencesAsync(facilityId, resourceType, cancellationToken);
            foreach (var id in existingOperationIds.Append(operationId).Distinct().OrderBy(id => id))
            {
                await _operationSequenceQueries.LockOperationAsync(id, cancellationToken);
            }

            existingOperationIds = await _operationSequenceQueries.OperationsInFacilitySequencesAsync(facilityId, resourceType, cancellationToken);
            if (!existingOperationIds.Contains(operationId))
            {
                var existing = await _database.OperationSequences.FindAsync(
                    sequence => sequence.FacilityId == facilityId && sequence.OperationResourceType.ResourceType.Name == resourceType,
                    cancellationToken);
                var resource = await _database.ResourceTypes.SingleOrDefaultAsync(candidate => candidate.Name == resourceType, cancellationToken);
                if (resource == null)
                {
                    throw new InvalidOperationException("No Resource Found.");
                }

                var operationResourceType = await _database.OperationResourceTypes.SingleAsync(
                    candidate => candidate.OperationId == operationId && candidate.ResourceTypeId == resource.Id,
                    cancellationToken);
                var nextSequence = existing.Count == 0 ? 1 : existing.Max(sequence => sequence.Sequence ?? 0) + 1;
                await _database.OperationSequences.AddAsync(new OperationSequence
                {
                    FacilityId = facilityId,
                    OperationResourceTypeId = operationResourceType.Id,
                    Sequence = nextSequence
                }, cancellationToken);
                await _database.SaveChangesAsync(cancellationToken);
                await _operationSequenceQueries.InvalidateFacilityAsync(facilityId, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return await _operationSequenceQueries.Search(new OperationSequenceSearchModel
            {
                FacilityId = facilityId,
                ResourceType = resourceType
            }, false, cancellationToken);
            }, cancellationToken);
        }

        public async Task<List<OperationSequenceModel>> CreateOperationSequences(CreateOperationSequencesModel model, CancellationToken cancellationToken = default)
        {
            if (model.OperationSequences.Count == 0)
            {
                throw new InvalidOperationException("No Operations provided.");
            }

            if (string.IsNullOrEmpty(model.FacilityId))
            {
                throw new InvalidOperationException("No FacilityId Provided");
            }

            if (!model.OperationSequences.All(s => s.Sequence > 0))
            {
                throw new InvalidOperationException("All Sequence values must be greater than 0");
            }

            if (model.OperationSequences.Select(os => os.Sequence).Distinct().Count() != model.OperationSequences.Count())
            {
                throw new InvalidOperationException("Repeated Sequence detected. Each sequence entry must have a unique numerical value that is greater than 0.");
            }

            if (model.OperationSequences.Select(s => s.OperationId).GroupBy(o => o).Any(g => g.Count() > 1))
            {
                throw new InvalidOperationException("Each Operation ID can only occur once in a given sequence");
            }

            return await ExecuteWithDeadlockRetryAsync(async () =>
            {
            await using var transaction = await _database.BeginTransactionAsync(cancellationToken);
            await _operationSequenceQueries.LockResourceTypeAsync(model.ResourceType, cancellationToken);
            await _operationSequenceQueries.LockFacilitySequenceWritesAsync(model.FacilityId, cancellationToken);
            var existingOperationIds = await _operationSequenceQueries.OperationsInFacilitySequencesAsync(model.FacilityId, model.ResourceType, cancellationToken);
            foreach (var operationId in existingOperationIds.Concat(model.OperationSequences.Select(sequence => sequence.OperationId)).Distinct().OrderBy(id => id))
            {
                await _operationSequenceQueries.LockOperationAsync(operationId, cancellationToken);
            }

            var existing = await _database.OperationSequences.FindAsync(s => s.FacilityId == model.FacilityId && s.OperationResourceType.ResourceType.Name == model.ResourceType, cancellationToken);

            existing.ForEach(_database.OperationSequences.Remove);

            var sequences = model.OperationSequences.OrderBy(s => s.Sequence).ToList();

            var resource = await _database.ResourceTypes.SingleOrDefaultAsync(r => r.Name == model.ResourceType, cancellationToken);

            if (resource == null)
            {
                throw new InvalidOperationException("No Resource Found.");
            }

            foreach (var sequence in sequences)
            {   
                var operation = await _database.Operations.SingleAsync(o => o.Id == sequence.OperationId, cancellationToken);
                var operationResourceTypeMap = await _database.OperationResourceTypes.SingleAsync(ort => ort.OperationId == operation.Id && ort.ResourceTypeId == resource.Id, cancellationToken);
                await _database.OperationSequences.AddAsync(new OperationSequence()
                {
                    FacilityId = model.FacilityId,
                    OperationResourceTypeId = operationResourceTypeMap.Id,
                    Sequence = sequence.Sequence,
                }, cancellationToken);
            }

            await _database.SaveChangesAsync(cancellationToken);
            await _operationSequenceQueries.InvalidateFacilityAsync(model.FacilityId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return await _operationSequenceQueries.Search(new OperationSequenceSearchModel()
            {
                FacilityId = model.FacilityId,
                ResourceType = model.ResourceType
            }, false, cancellationToken);
            }, cancellationToken);
        }

        private async Task<T> ExecuteWithDeadlockRetryAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
        {
            const int maxAttempts = 8;
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await action();
                }
                catch (Exception ex) when (attempt < maxAttempts && IsSqlDeadlock(ex))
                {
                    if (_database.HasActiveTransaction)
                    {
                        try
                        {
                            await _database.RollbackTransactionAsync(cancellationToken);
                        }
                        catch (Exception)
                        {
                            // SQL Server already aborted the deadlock victim.
                        }
                    }

                    _database.ClearChanges();
                    await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(25, 80) * attempt), cancellationToken);
                }
            }
        }

        private static bool IsSqlDeadlock(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current.GetType().Name == "SqlException"
                    && current.GetType().GetProperty("Number")?.GetValue(current) is int number
                    && number == 1205)
                {
                    return true;
                }

                if (current.Message.Contains("was deadlocked", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public async Task<bool> DeleteOperationSequence(DeleteOperationSequencesModel model, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(model.FacilityId))
            {
                throw new InvalidOperationException("No FacilityId Provided");
            }

            var resourceType = model.ResourceType ?? string.Empty;
            var ownsTransaction = !_database.HasActiveTransaction;
            var transaction = ownsTransaction ? await _database.BeginTransactionAsync(cancellationToken) : null;
            try
            {
                if (ownsTransaction)
                {
                    await LockResourceTypesBeforeFacilityAsync(model.FacilityId, model.ResourceType, cancellationToken);
                }

                await _operationSequenceQueries.LockFacilitySequenceWritesAsync(model.FacilityId, cancellationToken);
                if (model.OperationId.HasValue)
                {
                    await _operationSequenceQueries.LockOperationAsync(model.OperationId.Value, cancellationToken);
                }
                else
                {
                    var operationIds = await _operationSequenceQueries.OperationsInFacilitySequencesAsync(model.FacilityId, model.ResourceType, cancellationToken);
                    foreach (var operationId in operationIds.OrderBy(id => id))
                    {
                        await _operationSequenceQueries.LockOperationAsync(operationId, cancellationToken);
                    }
                }

                var sequences = await _database.OperationSequences.FindAsync(s => s.FacilityId == model.FacilityId
                                            && (resourceType == string.Empty || s.OperationResourceType.ResourceType.Name.Equals(model.ResourceType))
                                            && (model.OperationId == null || s.OperationResourceType.OperationId == model.OperationId), cancellationToken);

                if (!sequences.Any())
                {
                    return false;
                }

                sequences.ForEach(_database.OperationSequences.Remove);
                await _database.SaveChangesAsync(cancellationToken);
                // DeleteOperation joins this transaction and still has later operation locks to take.
                // Bumping the facility revision here would lock that row first and deadlock with
                // UpdateOperation, which locks the operation and then the revision. A joined caller
                // invalidates the facility after those operation locks. A caller that owns this
                // transaction has no later locks, so it bumps the revision before commit.
                if (transaction != null)
                {
                    await _operationSequenceQueries.InvalidateFacilityAsync(model.FacilityId, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }

                return true;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }
    }
}