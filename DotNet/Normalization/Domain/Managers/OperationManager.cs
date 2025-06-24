using Hl7.FhirPath.Expressions;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Queries;

namespace LantanaGroup.Link.Normalization.Domain.Managers
{
    public interface IOperationManager
    {
        Task<OperationModel> CreateOperation(CreateOperationModel model);
        Task<OperationModel?> UpdateOperation(UpdateOperationModel model);
        Task<bool> DeleteOperation(DeleteOperationModel deleteOperationModel);
        Task UpdateVendorPresetsForOperation(Guid operationId, List<Guid>? vendorIds);
        Task UpdateOperationResourceTypesForOperation(Guid operationId, List<ResourceModel> resources);
        Task UpdateOperationResourceTypesForOperation(Guid operationId, List<string> resourceTypes);
        Task<List<OperationSequenceModel>> CreateOperationSequences(CreateOperationSequencesModel model);
        Task<bool> DeleteOperationSequence(DeleteOperationSequencesModel deleteOperationSequencesModel);
    }

    public class OperationManager : IOperationManager
    {
        private readonly IDatabase _database;
        private readonly IResourceManager _resourceManager;
        private readonly IVendorManager _vendorManager;
        private readonly IVendorQueries _vendorQueries;
        private readonly IOperationQueries _operationQueries;
        private readonly IOperationSequenceQueries _operationSequenceQueries;
        private readonly IResourceQueries _resourceQueries; 

        public OperationManager(IDatabase database, IOperationQueries operationQueries, IVendorQueries vendorQueries, IOperationSequenceQueries operationSequenceQueries, IResourceQueries resourceQueries, IResourceManager resourceManager, IVendorManager vendorManager)
        {
            _database = database;
            _operationQueries = operationQueries;
            _operationSequenceQueries = operationSequenceQueries;
            _resourceQueries = resourceQueries;
            _resourceManager = resourceManager;
            _vendorManager = vendorManager;
            _vendorQueries = vendorQueries;
        }

        public async Task<OperationModel> CreateOperation(CreateOperationModel model)
        {
            var operation = new Operation()
            {
                OperationType = model.OperationType,
                OperationJson = model.OperationJson,
                FacilityId = model.FacilityId,
                Description = model.Description,
                IsDisabled = model.IsDisabled,
                CreateDate = DateTime.UtcNow,
                ModifyDate = null
            };

            await _database.Operations.AddAsync(operation);
            await _database.SaveChangesAsync();

            await UpdateOperationResourceTypesForOperation(operation.Id, model.ResourceTypes);
            await UpdateVendorPresetsForOperation(operation.Id, model.VendorIds);

            return await _operationQueries.Get(operation.Id, operation.FacilityId);
        }

        public async Task<OperationModel?> UpdateOperation(UpdateOperationModel model)
        {
            var operation = await _database.Operations.GetAsync(model.Id);
            operation.OperationResourceTypes = await _database.OperationResourceTypes.FindAsync(m => m.OperationId == model.Id);

            if(operation == null)
            {
                return null;
            }

            operation.FacilityId = model.FacilityId;
            operation.Description = model.Description;
            operation.OperationJson = model.OperationJson;
            operation.IsDisabled = model.IsDisabled;
            operation.ModifyDate = DateTime.UtcNow;

            await _database.SaveChangesAsync();

            await UpdateOperationResourceTypesForOperation(model.Id, model.ResourceTypes);
            await UpdateVendorPresetsForOperation(model.Id, model.VendorIds);          

            return await _operationQueries.Get(operation.Id, operation.FacilityId);
        }

        public async Task UpdateVendorPresetsForOperation(Guid operationId, List<Guid>? vendorIds)
        {
            if (vendorIds != null)
            {
                var orts = (await _database.OperationResourceTypes.FindAsync(m => m.OperationId == operationId)).Select(ort => ort.Id);

                foreach (var ort in orts)
                {
                    //Delete the Vendor Operation Prests that are no longer tied to this operation.
                    var toDelete = await _database.VendorVersionOperationPresets.FindAsync(vp => vp.OperationResourceTypeId == ort && !vendorIds.Contains(vp.VendorVersion.VendorId));

                    foreach (var delete in toDelete)
                    {
                        _database.VendorVersionOperationPresets.Remove(delete);
                    }

                    //Create the ones that don't exist
                    foreach (var vendorId in vendorIds)
                    {
                        if (!await _database.VendorVersionOperationPresets.AnyAsync(vop => vop.VendorVersion.VendorId == vendorId && vop.OperationResourceTypeId == ort))
                        {
                            var version = await _database.VendorVersions.FirstAsync(vv => vv.VendorId == vendorId);
                            await _database.VendorVersionOperationPresets.AddAsync(new VendorVersionOperationPreset()
                            {
                                OperationResourceTypeId = ort,
                                VendorVersionId = version.Id
                            });
                        }
                    }
                }

                await _database.SaveChangesAsync();
            }
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

        public async Task<bool> DeleteOperation(DeleteOperationModel model)
        {
            if (string.IsNullOrEmpty(model.FacilityId) && model.VendorId == null)
            {
                throw new InvalidOperationException("Request must include a valid facilityId and/or vendor");
            }

            using var transaction = await _database.BeginTransactionAsync();

            var modifiedRecords = 0;

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
                        VendorId = model.VendorId,
                        OperationId = model.OperationId,
                        ResourceType = model.ResourceType,
                        IncludeDisabled = true
                    });

                    if (operations != null && operations.Records.Count > 0)
                    {
                        modifiedRecords += operations.Records.Count;

                        returned = operations.Records.Count;
                        count = operations.Metadata.TotalCount;

                        foreach (var operation in operations.Records)
                        {
                            var op = await _database.Operations.GetAsync(operation.Id);
                            _database.Operations.Remove(op);
                        }

                        await _database.SaveChangesAsync();
                    }

                } while (count > returned);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            if (modifiedRecords > 0)
            {
                await transaction.CommitAsync();
                return true;
            }

            return false;
        }

        public async Task<List<OperationSequenceModel>> CreateOperationSequences(CreateOperationSequencesModel model)
        {
            if (model.OperationSequences.Count == 0)
            {
                throw new InvalidOperationException("No Operations provided.");
            }

            if(string.IsNullOrEmpty(model.FacilityId))
            {
                throw new InvalidOperationException("No FacilityId Provided");
            }

            if(!model.OperationSequences.All(s => s.Sequence > 0))
            {
                throw new InvalidOperationException("All Sequence values must be greater than 0");
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

            await _database.SaveChangesAsync();

            return await _operationSequenceQueries.Search(new OperationSequenceSearchModel()
            {
                FacilityId = model.FacilityId,
                ResourceType = model.ResourceType
            });
        }

        public async Task<bool> DeleteOperationSequence(DeleteOperationSequencesModel model)
        {
            if (string.IsNullOrEmpty(model.FacilityId))
            {
                throw new InvalidOperationException("No FacilityId Provided");
            }

            var resourceType = model.ResourceType ?? string.Empty;

            var sequences = await _database.OperationSequences.FindAsync(s => s.FacilityId == model.FacilityId && (resourceType == string.Empty || s.OperationResourceType.ResourceType.Name.Equals(model.ResourceType)));

            if (sequences.Any())
            {
                sequences.ForEach(_database.OperationSequences.Remove);

                await _database.SaveChangesAsync();

                return true;
            }

            return false;
        }
    }
}
