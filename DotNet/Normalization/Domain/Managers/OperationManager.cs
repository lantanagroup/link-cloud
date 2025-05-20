using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Queries;

namespace LantanaGroup.Link.Normalization.Domain.Managers
{
    public interface IOperationManager
    {
        Task<OperationModel> CreateOperation(CreateOperationModel model);
        Task<OperationModel?> UpdateOperation(UpdateOperationModel model);
    }

    public class OperationManager : IOperationManager
    {
        private readonly IDatabase _database;
        private readonly IOperationQueries _operationQueries;
        public OperationManager(IDatabase database, IOperationQueries operationQueries)
        {
            _database = database;
            _operationQueries = operationQueries;
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
                ResourceTypeId = t.Id
            }).ToList();

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
    }
}
