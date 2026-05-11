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
        Task<ResourceModel> CreateResource(string resourceName, bool bypassTypeCheck = false);
        Task DeleteResource(string resource);
    }

    public class ResourceManager : IResourceManager
    {
        private readonly IDatabase _database;
        private readonly IResourceQueries _resourceQueries;
        private readonly ILogger<ResourceManager> _logger;
        public ResourceManager(IDatabase database, IResourceQueries resourceQueries, ILogger<ResourceManager> logger)
        {
            _database = database;
            _resourceQueries = resourceQueries;
            _logger = logger;
        }

        public async Task<ResourceModel> CreateResource(string resourceName, bool bypassTypeCheck = false)
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

            var existing = await _resourceQueries.Get(resourceName);

            if (existing != null)
            {
                return null;
            }

            try
            {
                var resource = await _database.ResourceTypes.AddAsync(new Entities.ResourceType()
                {
                    Name = resourceName,
                });

                await _database.SaveChangesAsync();

                return await _resourceQueries.Get(resource.Id);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "DbUpdateException while creating ResourceType '{ResourceName}'. This may be a duplicate key race condition.", resourceName);
                return null;
            }
        }

        public async Task DeleteResource(string resource)
        {
            var resourceEntity = await _database.ResourceTypes.FindAsync(r => r.Name == resource);

            if (resourceEntity == null || resourceEntity.Count > 1 || resourceEntity.Count == 0)
            {
                throw new InvalidOperationException("An Error has occurred while deleting the Resource.");
            }

            _database.ResourceTypes.Remove(resourceEntity.Single());

            await _database.SaveChangesAsync();
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
