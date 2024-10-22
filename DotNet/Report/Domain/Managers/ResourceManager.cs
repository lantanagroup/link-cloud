using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.ResourceCategories;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Report.Domain.Managers
{
    public interface IResourceManager
    {
        Task<IFacilityResource?> GetResourceAsync(string facilityId, string resourceId, string resourceType,
            string patientId = "", CancellationToken cancellationToken = default);

        Task<IFacilityResource> UpdateResourceAsync(IFacilityResource resource, CancellationToken cancellationToken);

        Task<IFacilityResource> CreateResourceAsync(string facilityId, Resource resource, string patientId = "",
            CancellationToken cancellationToken = default);
    }

    public class ResourceManager : IResourceManager
    {
        private readonly IDatabase _database;

        public ResourceManager(IDatabase database)
        {
            _database = database;
        }

        public async Task<IFacilityResource?> GetResourceAsync(string facilityId, string resourceId, string resourceType, string patientId = "", CancellationToken cancellationToken = default)
        {
            var resourceTypeCategory = ResourceCategory.GetResourceCategoryByType(resourceType);

            if (resourceTypeCategory == null)
            {
                throw new Exception(resourceType + " is not a valid FHIR resouce");
            }

            if (resourceTypeCategory == ResourceCategoryType.Patient)
            {
                var patientResource = (await _database.PatientResourceRepository.FindAsync(
                    r => r.FacilityId == facilityId && r.PatientId == patientId && r.ResourceId == resourceId &&
                         r.ResourceType == resourceType, cancellationToken)).SingleOrDefault();

                return patientResource;
            }

            var sharedResource = (await _database.SharedResourceRepository.FindAsync(
                r => r.FacilityId == facilityId && r.ResourceId == resourceId &&
                     r.ResourceType == resourceType, cancellationToken)).SingleOrDefault();

            return sharedResource;
        }

        public async Task<IFacilityResource> UpdateResourceAsync(IFacilityResource resource, CancellationToken cancellationToken)
        {
            if (resource.GetType() == typeof(PatientResourceModel))
            {
                var patientResource = (PatientResourceModel)resource;
                patientResource.ModifyDate = DateTime.UtcNow;

                patientResource = await _database.PatientResourceRepository.UpdateAsync(patientResource, cancellationToken);

                return patientResource;
            }
            else if (resource.GetType() == typeof(SharedResourceModel))
            {
                var sharedResource = (SharedResourceModel)resource;
                sharedResource.ModifyDate = DateTime.UtcNow;

                sharedResource = await _database.SharedResourceRepository.UpdateAsync(sharedResource, cancellationToken);

                return sharedResource;
            }

            throw new DeadLetterException("parameter resource is not of an expected type");
        }

        public async Task<IFacilityResource> CreateResourceAsync(string facilityId, Resource resource, string patientId = "", CancellationToken cancellationToken = default)
        {
            var resourceTypeCategory = ResourceCategory.GetResourceCategoryByType(resource.TypeName);

            if (resourceTypeCategory == null)
            {
                throw new DeadLetterException(resource.TypeName + " is not a valid FHIR resouce");
            }

            if (resourceTypeCategory == ResourceCategoryType.Patient)
            {
                var patientResource = new PatientResourceModel()
                {
                    Id = Guid.NewGuid().ToString(),
                    FacilityId = facilityId,
                    PatientId = patientId,
                    Resource = resource,
                    ResourceId = resource.Id,
                    ResourceType = resource.TypeName,
                    CreateDate = DateTime.UtcNow
                };

                patientResource = await _database.PatientResourceRepository.AddAsync(patientResource, cancellationToken);

                return patientResource;
            }
            else
            {
                var sharedResource = new SharedResourceModel()
                {
                    Id = Guid.NewGuid().ToString(),
                    FacilityId = facilityId,
                    Resource = resource,
                    ResourceId = resource.Id,
                    ResourceType = resource.TypeName,
                    CreateDate = DateTime.UtcNow
                };

                sharedResource = await _database.SharedResourceRepository.AddAsync(sharedResource, cancellationToken);

                return sharedResource;
            }
        }
    }
}
