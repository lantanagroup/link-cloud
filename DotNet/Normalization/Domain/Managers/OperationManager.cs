﻿using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Normalization.Domain.Services;
using LantanaGroup.Link.Normalization.Application.Operations;

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
        Task UpdateOperationResourceTypesForOperation(Guid operationId, List<ResourceModel> resources);
        Task UpdateOperationResourceTypesForOperation(Guid operationId, List<string> resourceTypes);
        Task<List<OperationSequenceModel>> CreateOperationSequences(CreateOperationSequencesModel model, CancellationToken cancellationToken = default);
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
                    await _database.Operations.AnyAsync(operation => operation.FacilityId == model.FacilityId && operation.OperationType == "HSLOCMap"))
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

                var operation = new Operation()
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

                await _database.Operations.AddAsync(operation);
                await _database.SaveChangesAsync(cancellationToken);

                await UpdateOperationResourceTypesForOperation(operation.Id, model.ResourceTypes);
                await UpdateVendorPresetsForOperation(operation.Id, model.VendorVersionIds, cancellationToken);

                taskResult.IsSuccess = true;
                taskResult.ObjectResult = await _operationQueries.Get(operation.Id, operation.FacilityId);
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
                await _operationSequenceQueries.LockOperationAsync(model.Id, cancellationToken);

                #region Lookup the Detailed Operation Model to check for Facility/Vendor operation conversions (which are not allowed)
                var operationModel = (await _operationQueries.Search(new OperationSearchModel()
                {
                    OperationId = model.Id,
                    IncludeDisabled = true
                })).Records.SingleOrDefault();

                if (operationModel == null)
                {
                    throw new InvalidOperationException($"No Operation Found for Id {model.Id}");
                }

                if (!string.IsNullOrEmpty(operationModel.FacilityId) && (model.VendorVersionIds?.Any() ?? false))
                {
                    throw new InvalidOperationException("The operation for the provided Id is a facility operation, but the update model has provided vendor version IDs. A facility operation cannot also be a vendor operation or vice versa. Create a new Operation for the facility or vendor version(s).");
                }

                if (operationModel.VendorPresets.Any() && !string.IsNullOrEmpty(model.FacilityId))
                {
                    throw new InvalidOperationException("The operation for the provided Id is a vendor operation, but the update model has provided a FacilityId. A facility operation cannot also be a vendor operation or vice versa. Create a new Operation for the facility or vendor(s).");
                }
                #endregion

                var operation = await _database.Operations.GetAsync(model.Id);
                if (operation.OperationType == "HSLOCMap" && !string.IsNullOrEmpty(model.FacilityId) &&
                    await _database.Operations.AnyAsync(existing => existing.FacilityId == model.FacilityId && existing.OperationType == "HSLOCMap" && existing.Id != model.Id))
                {
                    taskResult.IsSuccess = false;
                    taskResult.ObjectResult = null;
                    taskResult.ErrorMessage = "Only one HSLOC Map operation is allowed per facility.";
                    return taskResult;
                }

                operation.OperationResourceTypes = await _database.OperationResourceTypes.FindAsync(m => m.OperationId == model.Id);

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
                await UpdateOperationResourceTypesForOperation(model.Id, model.ResourceTypes);
                await UpdateVendorPresetsForOperation(model.Id, model.VendorVersionIds, cancellationToken);
                await _operationSequenceQueries.InvalidateFacilitiesAsync(affectedFacilities, cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                taskResult.IsSuccess = true;
                taskResult.ObjectResult = await _operationQueries.Get(operation.Id, operation.FacilityId);
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

            if (_database.HasActiveTransaction)
            {
                await SaveVendorPresetsAsync(operationId, vendorVersionIds, cancellationToken);
                return;
            }

            await using var transaction = await _database.BeginTransactionAsync(cancellationToken);
            await SaveVendorPresetsAsync(operationId, vendorVersionIds, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        private async Task SaveVendorPresetsAsync(Guid operationId, List<Guid> vendorVersionIds, CancellationToken cancellationToken)
        {
            var resolvedVendorVersions = await _vendorVersionResolver.ResolveAsync(vendorVersionIds, cancellationToken);
            var orts = (await _database.OperationResourceTypes.FindAsync(m => m.OperationId == operationId)).Select(ort => ort.Id);

            foreach (var ort in orts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var toDelete = await _database.VendorVersionOperationPresets.FindAsync(vp => vp.OperationResourceTypeId == ort && !vendorVersionIds.Contains(vp.VendorVersionId));

                foreach (var delete in toDelete)
                {
                    _database.VendorVersionOperationPresets.Remove(delete);
                }

                foreach (var vendorVersionId in resolvedVendorVersions.Keys)
                {
                    if (!await _database.VendorVersionOperationPresets.AnyAsync(vop => vop.VendorVersionId == vendorVersionId && vop.OperationResourceTypeId == ort))
                    {
                        await _database.VendorVersionOperationPresets.AddAsync(new VendorVersionOperationPreset()
                        {
                            OperationResourceTypeId = ort,
                            VendorVersionId = vendorVersionId
                        });
                    }
                }
            }

            await _database.SaveChangesAsync(cancellationToken);
            await InvalidateCachedSequencesAsync(operationId, cancellationToken);
        }

        public async Task UpdateOperationResourceTypesForOperation(Guid operationId, List<string> resourceTypes)
        {
            if (resourceTypes == null || resourceTypes.Count == 0)
            {
                throw new InvalidOperationException("ResourceTypes must be provided.");
            }

            List<ResourceModel> resources = new List<ResourceModel>();
            foreach (var res in resourceTypes)
            {
                if (string.IsNullOrEmpty(res)) continue;

                var resource = await _resourceQueries.Get(res);

                if (resource == null)
                {
                    resource = await _resourceManager.CreateResource(res);
                }

                resources.Add(resource);
            }

            if (resources.Count != resourceTypes.Where(r => !string.IsNullOrEmpty(r)).Count())
            {
                throw new InvalidOperationException("Not all provided Resource Types were found.");
            }

            await UpdateOperationResourceTypesForOperation(operationId, resources);
        }

        public async Task UpdateOperationResourceTypesForOperation(Guid operationId, List<ResourceModel> resources)
        {
            var operation = await _database.Operations.GetAsync(operationId);
            operation.OperationResourceTypes = await _database.OperationResourceTypes.FindAsync(m => m.OperationId == operationId);

            //Delete any OperationResourceTypes that exist in the DB but not on the incoming model
            foreach (var ort in operation.OperationResourceTypes)
            {
                if (!resources.Any(r => r.ResourceTypeId == ort.ResourceTypeId))
                {
                    var sequences = await _database.OperationSequences.FindAsync(os => os.OperationResourceTypeId == ort.Id);
                    sequences.ForEach(_database.OperationSequences.Remove);

                    var vops = await _database.VendorVersionOperationPresets.FindAsync(vop => vop.OperationResourceTypeId == ort.Id);
                    vops.ForEach(_database.VendorVersionOperationPresets.Remove);

                    _database.OperationResourceTypes.Remove(ort);
                }
            }

            //Create any OperationResourceTypes that exist on the incoming model but not in the DB
            foreach (var resource in resources)
            {
                if (!operation.OperationResourceTypes.Any(ort => ort.ResourceTypeId == resource.ResourceTypeId && ort.Operation.Id == operation.Id))
                {
                    var ort = new OperationResourceType()
                    {
                        OperationId = operation.Id,
                        ResourceTypeId = resource.ResourceTypeId
                    };

                    operation.OperationResourceTypes.Add(ort);
                    await _database.OperationResourceTypes.AddAsync(ort);
                }
            }

            await _database.SaveChangesAsync();
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

            using var transaction = await _database.BeginTransactionAsync(cancellationToken);

            var modifiedRecords = 0;
            var affectedFacilities = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(model.FacilityId))
            {
                affectedFacilities.Add(model.FacilityId);
            }

            try
            {
                int returned;
                long count;

                do
                {
                    returned = 0;
                    count = 0;

                    var operations = await _operationQueries.Search(new OperationSearchModel()
                    {
                        FacilityId = model.FacilityId,
                        VendorVersionId = model.VendorVersionId,
                        OperationId = model.OperationId,
                        ResourceType = model.ResourceType,
                        IncludeDisabled = true
                    });

                    if (operations != null && operations.Records.Count > 0)
                    {
                        modifiedRecords += operations.Records.Count;

                        returned = operations.Records.Count;
                        count = operations.Metadata.TotalCount;

                        foreach (var operation in operations.Records.OrderBy(candidate => candidate.Id))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            await _operationSequenceQueries.LockOperationAsync(operation.Id, cancellationToken);
                            foreach (var facilityId in await _operationSequenceQueries.FacilitiesReferencingOperationAsync(operation.Id, cancellationToken))
                            {
                                affectedFacilities.Add(facilityId);
                            }

                            if (!string.IsNullOrEmpty(model.FacilityId))
                            {
                                await DeleteOperationSequence(new DeleteOperationSequencesModel()
                                {
                                    FacilityId = model.FacilityId,
                                    OperationId = operation.Id,
                                }, cancellationToken);
                            }

                            var orts = await _database.OperationResourceTypes.FindAsync(ort => ort.OperationId == operation.Id);
                            orts.ForEach(_database.OperationResourceTypes.Remove);

                            var vops = await _database.VendorVersionOperationPresets.FindAsync(vop => vop.OperationResourceType.OperationId == operation.Id);
                            vops.ForEach(_database.VendorVersionOperationPresets.Remove);

                            var op = await _database.Operations.GetAsync(operation.Id);
                            _database.Operations.Remove(op);
                        }

                        await _database.SaveChangesAsync(cancellationToken);
                    }

                } while (count > returned);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            if (modifiedRecords > 0)
            {
                await _operationSequenceQueries.InvalidateFacilitiesAsync(affectedFacilities, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return true;
            }

            return false;
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

            await using var transaction = await _database.BeginTransactionAsync(cancellationToken);
            foreach (var operationId in model.OperationSequences.Select(sequence => sequence.OperationId).Distinct().OrderBy(id => id))
            {
                await _operationSequenceQueries.LockOperationAsync(operationId, cancellationToken);
            }

            var existing = await _database.OperationSequences.FindAsync(s => s.FacilityId == model.FacilityId && s.OperationResourceType.ResourceType.Name == model.ResourceType);

            existing.ForEach(_database.OperationSequences.Remove);

            var sequences = model.OperationSequences.OrderBy(s => s.Sequence).ToList();

            var resource = await _database.ResourceTypes.SingleOrDefaultAsync(r => r.Name == model.ResourceType);

            if (resource == null)
            {
                throw new InvalidOperationException("No Resource Found.");
            }

            foreach (var sequence in sequences)
            {   
                var operation = await _database.Operations.SingleAsync(o => o.Id == sequence.OperationId);
                var operationResourceTypeMap = await _database.OperationResourceTypes.SingleAsync(ort => ort.OperationId == operation.Id && ort.ResourceTypeId == resource.Id);
                await _database.OperationSequences.AddAsync(new OperationSequence()
                {
                    FacilityId = model.FacilityId,
                    OperationResourceTypeId = operationResourceTypeMap.Id,
                    Sequence = sequence.Sequence,
                });
            }

            await _database.SaveChangesAsync(cancellationToken);
            await _operationSequenceQueries.InvalidateFacilityAsync(model.FacilityId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return await _operationSequenceQueries.Search(new OperationSequenceSearchModel()
            {
                FacilityId = model.FacilityId,
                ResourceType = model.ResourceType
            }, false, cancellationToken);
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
                if (model.OperationId.HasValue)
                {
                    await _operationSequenceQueries.LockOperationAsync(model.OperationId.Value, cancellationToken);
                }
                else
                {
                    var operationIds = await _operationSequenceQueries.OperationsInFacilitySequencesAsync(model.FacilityId, model.ResourceType, cancellationToken);
                    foreach (var operationId in operationIds)
                    {
                        await _operationSequenceQueries.LockOperationAsync(operationId, cancellationToken);
                    }
                }

                var sequences = await _database.OperationSequences.FindAsync(s => s.FacilityId == model.FacilityId
                                            && (resourceType == string.Empty || s.OperationResourceType.ResourceType.Name.Equals(model.ResourceType))
                                            && (model.OperationId == null || s.OperationResourceType.OperationId == model.OperationId));

                if (!sequences.Any())
                {
                    return false;
                }

                sequences.ForEach(_database.OperationSequences.Remove);
                await _database.SaveChangesAsync(cancellationToken);
                await _operationSequenceQueries.InvalidateFacilityAsync(model.FacilityId, cancellationToken);
                if (transaction != null)
                {
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