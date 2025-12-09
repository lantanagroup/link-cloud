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
        Task<FhirResource> CreateResourceAsync(string facilityId, string reportScheduleId, List<string> reportTypes, Resource resource, string patientId = "", CancellationToken cancellationToken = default);
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

        public async Task<FhirResource> CreateResourceAsync(string facilityId, string reportScheduleId, List<string> reportTypes, Resource resource, string patientId = "", CancellationToken cancellationToken = default)
        {
            var resourceTypeCategory = ResourceCategory.GetResourceCategoryByType(resource.TypeName);

            if (resourceTypeCategory == null)
            {
                throw new DeadLetterException(resource.TypeName + " is not a valid FHIR resouce");
            }

            var fhirResource = new FhirResource()
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

            await CreateReportResourceMap(reportScheduleId, reportTypes, fhirResource.Id, cancellationToken);

            return fhirResource;
        }

        public async Task CreateReportResourceMap(string reportScheduleId, List<string> reportTypes, string fhirResourceId, CancellationToken cancellationToken = default)
        {
            var resourceMap = await _context.ReportScheduleResourceMaps.SingleOrDefaultAsync(r => r.ReportScheduleId == reportScheduleId && r.FhirResourceId == fhirResourceId);

            if (resourceMap == null)
            {
                await _context.ReportScheduleResourceMaps.AddAsync(resourceMap = new ReportScheduleResourceMap
                {
                    FhirResourceId = fhirResourceId,
                    ReportScheduleId = reportScheduleId,
                    ReportTypes = reportTypes,
                    CreateDate = DateTime.UtcNow,
                    ModifyDate = DateTime.UtcNow
                });

                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            foreach (var reportType in reportTypes) 
            {
                if(!resourceMap.ReportTypes.Contains(reportType))
                {
                    resourceMap.ReportTypes.Add(reportType);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

        }
    }
}