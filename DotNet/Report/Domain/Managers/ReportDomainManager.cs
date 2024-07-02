using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.ResourceCategories;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Report.Domain.Managers
{
    public class ReportDomainManager
    {
        private readonly IMongoDatabase _database;

        public readonly IEntityRepository<PatientResourceModel> PatientResourceRepository;
        public readonly IEntityRepository<SharedResourceModel> SharedResourceRepository;
        public readonly IEntityRepository<MeasureReportScheduleModel> ReportScheduledRepository;
        public readonly IEntityRepository<MeasureReportSubmissionEntryModel> SubmissionEntryRepository;
        public readonly IEntityRepository<MeasureReportConfigModel> ReportConfigRepository;
        public readonly IEntityRepository<MeasureReportSubmissionModel> ReportSubmissionRepository;

        public ReportDomainManager(IOptions<MongoConnection> mongoSettings, IEntityRepository<PatientResourceModel> patientResourceRepository, IEntityRepository<SharedResourceModel> sharedResourceRepository, IEntityRepository<MeasureReportScheduleModel> reportScheduledRepository,
            IEntityRepository<MeasureReportSubmissionEntryModel> submissionEntryRepository, IEntityRepository<MeasureReportConfigModel> reportConfigRepository, IEntityRepository<MeasureReportSubmissionModel> reportSubmissionRepository)
        {
            var client = new MongoClient(mongoSettings.Value.ConnectionString);
            _database = client.GetDatabase(mongoSettings.Value.DatabaseName);

            PatientResourceRepository = patientResourceRepository;
            SharedResourceRepository = sharedResourceRepository;
            ReportScheduledRepository = reportScheduledRepository;
            SubmissionEntryRepository = submissionEntryRepository;
            ReportConfigRepository = reportConfigRepository;
            ReportSubmissionRepository = reportSubmissionRepository;
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
                var patientResource = (await PatientResourceRepository.FindAsync(
                    r => r.FacilityId == facilityId && r.PatientId == patientId && r.ResourceId == resourceId &&
                         r.ResourceType == resourceType, cancellationToken)).SingleOrDefault();

                return patientResource;
            }

            var sharedResource = (await SharedResourceRepository.FindAsync(
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

                patientResource = await PatientResourceRepository.UpdateAsync(patientResource, cancellationToken);

                return patientResource;
            }
            else if(resource.GetType() == typeof(SharedResourceModel))
            {
                var sharedResource = (SharedResourceModel)resource;
                sharedResource.ModifyDate = DateTime.UtcNow;

                sharedResource = await SharedResourceRepository.UpdateAsync(sharedResource, cancellationToken);

                return sharedResource;
            }

            throw new DeadLetterException("parameter resource is not of an expected type", AuditEventType.Update);
        }

        public async Task<IFacilityResource> CreateResourceAsync(string facilityId, Resource resource, string patientId = "", CancellationToken cancellationToken = default)
        {
            var resourceTypeCategory = ResourceCategory.GetResourceCategoryByType(resource.TypeName);

            if (resourceTypeCategory == null)
            {
                throw new DeadLetterException(resource.TypeName + " is not a valid FHIR resouce", AuditEventType.Create);
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
                    ResourceType = resource.TypeName
                };

                patientResource = await PatientResourceRepository.AddAsync(patientResource, cancellationToken);
                
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
                    ResourceType = resource.TypeName
                };

                sharedResource = await SharedResourceRepository.AddAsync(sharedResource, cancellationToken);

                return sharedResource;
            }
        }

        public async Task<MeasureReportScheduleModel?> GetMeasureReportSchedule(string facilityId, DateTime startDate, DateTime endDate, string reportType, CancellationToken cancellationToken = default)
        {
            // find existing report scheduled for this facility, report type, and date range
            return (await ReportScheduledRepository.FindAsync(
                r => r.FacilityId == facilityId && r.ReportStartDate == startDate && r.ReportEndDate == endDate &&
                     r.ReportType == reportType, cancellationToken))?.SingleOrDefault();
        }

        public async Task<List<MeasureReportScheduleModel>?> GetMeasureReportSchedules(string facilityId, DateTime startDate, DateTime endDate,CancellationToken cancellationToken = default)
        {
            // find existing report scheduled for this facility, report type, and date range
            return (await ReportScheduledRepository.FindAsync(
                r => r.FacilityId == facilityId && r.ReportStartDate == startDate && r.ReportEndDate == endDate, cancellationToken))?.ToList();
        }

        public async Task<MeasureReportSubmissionEntryModel?> GetPatientSubmissionEntry(string measureReportScheduleId, string patientId, CancellationToken cancellationToken = default)
        {
            // find existing report scheduled for this facility, report type, and date range
            return (await SubmissionEntryRepository.FindAsync(s => s.MeasureReportScheduleId == measureReportScheduleId && s.PatientId == patientId, cancellationToken))?.SingleOrDefault();
        }
    }
}
