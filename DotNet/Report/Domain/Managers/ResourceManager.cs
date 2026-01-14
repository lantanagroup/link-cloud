using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.ResourceCategories;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Report.Domain.Managers
{
    public interface IResourceManager
    {
        Task<FhirResource> UpdateResourceAsync(FhirResource resource, CancellationToken cancellationToken = default);
        Task<FhirResource> CreateResourceAsync(string facilityId, string reportScheduleId, string submissionEntryId, List<string> reportTypes, Resource resource, string? patientId = null, CancellationToken cancellationToken = default);
        Task CreateSubmissionEntryResourceMap(string reportScheduleId, string submissionEntryId, List<string> reportTypes, string resourceType, string resourceId, string? fhirResourceId = null, bool performSave = true, CancellationToken cancellationToken = default);
    }

    public class ResourceManager : IResourceManager
    {
        private readonly MongoDbContext _context;

        public ResourceManager(MongoDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<FhirResource> UpdateResourceAsync(FhirResource fhirResource, CancellationToken cancellationToken = default)
        {
            fhirResource.ModifyDate = DateTime.UtcNow;

            _context.FhirResources.Update(fhirResource);

            await _context.SaveChangesAsync(cancellationToken);

            return fhirResource;
        }

        public async Task<FhirResource> CreateResourceAsync(string facilityId, string reportScheduleId, string submissionEntryId, List<string> reportTypes, Resource resource, string? patientId = null, CancellationToken cancellationToken = default)
        {
            var resourceTypeCategory = ResourceCategory.GetResourceCategoryByType(resource.TypeName);

            if (resourceTypeCategory == null)
            {
                throw new DeadLetterException(resource.TypeName + " is not a valid FHIR resouce");
            }

            var fhirResource = await _context.FhirResources.SingleOrDefaultAsync(r => r.ResourceId == resource.Id 
                                                                                    && r.ResourceType == resource.TypeName 
                                                                                    && r.FacilityId == facilityId 
                                                                                    && (resourceTypeCategory.Value == ResourceCategoryType.Shared || r.PatientId == patientId));

            if (fhirResource == null)
            {
                fhirResource = new FhirResource()
                {
                    FacilityId = facilityId,
                    PatientId = resourceTypeCategory == ResourceCategoryType.Patient ? patientId : null,
                    Resource = resource,
                    ResourceId = resource.Id,
                    ResourceType = resource.TypeName,
                    ResourceCategoryType = (ResourceCategoryType)resourceTypeCategory,
                    CreateDate = DateTime.UtcNow
                };

                await _context.FhirResources.AddAsync(fhirResource, cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);
            }

            await CreateSubmissionEntryResourceMap(reportScheduleId, submissionEntryId, reportTypes, fhirResource.ResourceType, fhirResource.ResourceId, fhirResource.Id, true, cancellationToken);

            return fhirResource;
        }

        public async Task CreateSubmissionEntryResourceMap(string reportScheduleId, string submissionEntryId, List<string> reportTypes, string resourceType, string resourceId, string? fhirResourceId = null, bool performSave = true, CancellationToken cancellationToken = default)
        {
            var resourceMap = await _context.PatientEntryResourceMaps.SingleOrDefaultAsync(r => r.SubmissionEntryId == submissionEntryId && r.FhirResourceId == fhirResourceId);

            if (resourceMap == null)
            {
                await _context.PatientEntryResourceMaps.AddAsync(resourceMap = new PatientSubmissionEntryResourceMap
                {
                    ResourceType = resourceType,
                    ResourceId = resourceId,
                    ReportScheduleId = reportScheduleId,
                    FhirResourceId = fhirResourceId,
                    SubmissionEntryId = submissionEntryId,
                    ReportTypes = reportTypes,
                    CreateDate = DateTime.UtcNow,
                    ModifyDate = DateTime.UtcNow
                });
            }
            else
            {
                resourceMap.FhirResourceId = fhirResourceId;
                resourceMap.ModifyDate = DateTime.UtcNow;

                foreach (var reportType in reportTypes)
                {
                    if (!resourceMap.ReportTypes.Contains(reportType))
                    {
                        resourceMap.ReportTypes.Add(reportType);
                    }
                }
            }

            if (performSave)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}