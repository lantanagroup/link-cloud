using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Normalization.Domain.Managers
{
    public interface IResourceManager
    {
        Task<List<ResourceModel>> InitializeResources();
        Task<ResourceModel> CreateResource(string resourceName, bool bypassTypeCheck = false, CancellationToken cancellationToken = default);
        Task DeleteResource(string resource, CancellationToken cancellationToken = default);
    }

    public class ResourceManager : IResourceManager
    {
        private static readonly SemaphoreSlim CreateResourceLock = new(1, 1);

        private readonly IDatabase _database;
        private readonly IResourceQueries _resourceQueries;
        private readonly IOperationSequenceQueries _operationSequenceQueries;
        private readonly ILogger<ResourceManager> _logger;
        public ResourceManager(IDatabase database, IResourceQueries resourceQueries, IOperationSequenceQueries operationSequenceQueries, ILogger<ResourceManager> logger)
        {
            _database = database;
            _resourceQueries = resourceQueries;
            _operationSequenceQueries = operationSequenceQueries;
            _logger = logger;
        }

        public async Task<ResourceModel> CreateResource(string resourceName, bool bypassTypeCheck = false, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
            {
                throw new InvalidOperationException("Provided resource name cannot be null, empty, or whitepsace.");
            }

            if (!bypassTypeCheck)
            {
                ResourceType resourceType;
                if (!Enum.TryParse(resourceName, ignoreCase: true, out resourceType))
                {
                    throw new InvalidOperationException($"'{resourceName.Sanitize()}' is not a valid ResourceType.");
                }

                resourceName = resourceType.ToString();
            }

            await CreateResourceLock.WaitAsync(cancellationToken);
            try
            {
                var existing = await _resourceQueries.Get(resourceName, cancellationToken);

                if (existing != null)
                {
                    return existing;
                }

                var entity = new Entities.ResourceType() { Name = resourceName };
                await _database.ResourceTypes.AddAsync(entity, cancellationToken);
                await _database.SaveChangesAsync(cancellationToken);

                return await _resourceQueries.Get(resourceName, cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                var sanitizedResourceName = resourceName.Replace("\r", string.Empty).Replace("\n", string.Empty);
                _logger.LogWarning(ex, "DbUpdateException while creating ResourceType '{ResourceName}'. This may be a duplicate key race condition.", sanitizedResourceName);

                var existing = await _resourceQueries.Get(resourceName, cancellationToken);
                if (existing != null)
                {
                    return existing;
                }

                throw;
            }
            finally
            {
                CreateResourceLock.Release();
            }
        }

        public async Task DeleteResource(string resource, CancellationToken cancellationToken = default)
        {
            var resourceEntity = await _database.ResourceTypes.FindAsync(r => r.Name == resource, cancellationToken);

            if (resourceEntity == null || resourceEntity.Count > 1 || resourceEntity.Count == 0)
            {
                throw new InvalidOperationException("An Error has occurred while deleting the Resource.");
            }

            await using var transaction = await _database.BeginTransactionAsync(cancellationToken);
            await _operationSequenceQueries.LockResourceTypeAsync(resource, cancellationToken);
            var affectedFacilities = await _operationSequenceQueries.FacilitiesUsingResourceTypeAsync(resource, cancellationToken);
            foreach (var facilityId in affectedFacilities.OrderBy(id => id, StringComparer.Ordinal))
            {
                await _operationSequenceQueries.LockFacilitySequenceWritesAsync(facilityId, cancellationToken);
            }

            _database.ResourceTypes.Remove(resourceEntity.Single());
            await _database.SaveChangesAsync(cancellationToken);
            await _operationSequenceQueries.InvalidateFacilitiesAsync(affectedFacilities, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public async Task<List<ResourceModel>> InitializeResources()
        {
            List<string> resources = new List<string>(Enum.GetNames(typeof(ResourceType)));

            List<ResourceModel> resourceModels = new();
            foreach (var resource in resources)
            {
                var created = await CreateResource(resource);
                if (created != null)
                {
                    resourceModels.Add(created);
                }
            }

            return resourceModels;
        }
    }
}
