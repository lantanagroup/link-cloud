using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Queries;

namespace LantanaGroup.Link.Normalization.Domain.Managers
{
    public interface IOperationManager
    {
        Task<OperationModel> CreateOperation(CreateOperationModel model);
        Task<OperationModel?> UpdateOperation(UpdateOperationModel model);
        Task<bool> DeleteOperation(DeleteOperationModel deleteOperationModel);


        Task<List<OperationSequenceModel>> CreateOperationSequences(CreateOperationSequencesModel model);
        Task<bool> DeleteOperationSequence(DeleteOperationSequencesModel deleteOperationSequencesModel);
    }

    public class OperationManager : IOperationManager
    {
        private readonly IDatabase _database;
        private readonly IOperationQueries _operationQueries;
        private readonly IOperationSequenceQueries _operationSequenceQueries;
        public OperationManager(IDatabase database, IOperationQueries operationQueries, IOperationSequenceQueries operationSequenceQueries)
        {
            _database = database;
            _operationQueries = operationQueries;
            _operationSequenceQueries = operationSequenceQueries;
        }

        public async Task<OperationModel> CreateOperation(CreateOperationModel model)
        {
            var resourceTypes = await _database.ResourceTypes.FindAsync(r => model.ResourceTypes.Contains(r.Name));

            if(resourceTypes.Count == 0)
            {
                throw new InvalidOperationException("No Resource Types Found.");
            }

            else if(resourceTypes.Count != model.ResourceTypes.Count)
            {
                throw new InvalidOperationException("Not all provided Resource Types were found.");
            }

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

            operation.OperationResourceTypes = resourceTypes.Select(t => new OperationResourceType()
            {
                OperationId = operation.Id,
                ResourceTypeId = t.Id,                
            }).ToList();

            await _database.SaveChangesAsync();

            if (model.VendorPresetIds != null)
            {
                foreach (var ort in operation.OperationResourceTypes)
                {
                    foreach (var presetId in model.VendorPresetIds)
                    {
                        ort.VendorPresetOperationResourceTypes.Add(new VendorPresetOperationResourceType()
                        {
                            OperationResourceTypeId = ort.Id,
                            VendorOperationPresetId = presetId
                        });
                    }
                }
            }

            await _database.SaveChangesAsync();

            return await _operationQueries.Get(operation.Id, operation.FacilityId);
        }

        public async Task<OperationModel?> UpdateOperation(UpdateOperationModel model)
        {
            var resourceTypes = await _database.ResourceTypes.FindAsync(r => model.ResourceTypes.Contains(r.Name));

            if (resourceTypes.Count == 0)
            {
                throw new InvalidOperationException("No Resource Types Found.");
            }
            else if (resourceTypes.Count != model.ResourceTypes.Count)
            {
                throw new InvalidOperationException("Not all provided Resource Types were found.");
            }

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

            operation.OperationResourceTypes.Clear();
            operation.OperationResourceTypes = resourceTypes.Select(t => new OperationResourceType()
            {
                OperationId = operation.Id,
                ResourceTypeId = t.Id
            }).ToList();

            operation.ModifyDate = DateTime.UtcNow;

            await _database.SaveChangesAsync();

            return await _operationQueries.Get(operation.Id, operation.FacilityId);
        }

        public async Task<bool> DeleteOperation(DeleteOperationModel model)
        {
            if (string.IsNullOrEmpty(model.FacilityId) && model.OperationId == null)
            {
                throw new InvalidOperationException("Request must include a valid facilityId and/or operationId");
            }

            int returned = 0;
            long count = 0;

            do {
                var operations = await _operationQueries.Search(new OperationSearchModel()
                {
                    FacilityId = model.FacilityId,
                    OperationId = model.OperationId,
                    ResourceType = model.ResourceType,

                });

                if (operations == null || operations.Records.Count == 0)
                {
                    return false;
                }

                returned = operations.Records.Count;
                count = operations.Metadata.TotalCount;

                foreach (var operation in operations.Records)
                {
                    var op = await _database.Operations.GetAsync(operation.Id);
                    _database.Operations.Remove(op);
                }

                await _database.SaveChangesAsync();

            } while (count > returned);

            return true;
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
