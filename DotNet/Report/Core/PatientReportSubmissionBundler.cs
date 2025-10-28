using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.ResourceCategories;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Report.Core
{
    /// <summary>
    /// This Class is used to generate a bundleSettings of a particular patients data for the provided facility and the report period.
    /// This bundleSettings will include data for all applicable Measure Reports as well as a separate bundleSettings of all resources that are not strictly "Patient" resources.
    /// </summary>
    public class PatientReportSubmissionBundler
    {
        private readonly ILogger<PatientReportSubmissionBundler> _logger;
        private readonly IReportServiceMetrics _metrics;
        private readonly IDatabase _database;
        private readonly IReportScheduledManager _reportScheduledManager;

        private readonly List<string> REMOVE_EXTENSIONS = new List<string> {
        "http://hl7.org/fhir/5.0/StructureDefinition/extension-MeasureReport.population.description",
        "http://hl7.org/fhir/5.0/StructureDefinition/extension-MeasureReport.supplementalDataElement.reference",
        "http://hl7.org/fhir/us/davinci-deqm/StructureDefinition/extension-criteriaReference",
        "http://open.epic.com/FHIR/StructureDefinition/extension/accidentrelated",
        "http://open.epic.com/FHIR/StructureDefinition/extension/epic-id",
        "http://open.epic.com/FHIR/StructureDefinition/extension/ip-admit-datetime",
        "http://open.epic.com/FHIR/StructureDefinition/extension/observation-datetime",
        "http://open.epic.com/FHIR/StructureDefinition/extension/specialty",
        "http://open.epic.com/FHIR/StructureDefinition/extension/team-name",
        "https://open.epic.com/FHIR/StructureDefinition/extension/patient-merge-unmerge-instant"};

        public PatientReportSubmissionBundler(ILogger<PatientReportSubmissionBundler> logger, IDatabase database, IReportServiceMetrics metrics, IReportScheduledManager reportScheduledManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metrics = metrics ?? throw new ArgumentException(nameof(metrics));
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _reportScheduledManager = reportScheduledManager ?? throw new ArgumentNullException(nameof(reportScheduledManager));
        }


        public async Task<PatientSubmissionModel> GenerateBundle(string facilityId, string patientId, string reportScheduleId)
        {
            var schedule = await _reportScheduledManager.SingleOrDefaultAsync(s => s.Id == reportScheduleId) ?? throw new Exception($"No Measure Reports Scheduled for reportScheduleId of {reportScheduleId}");

            var entries = await _database.SubmissionEntryRepository.FindAsync(e =>
                e.FacilityId == facilityId && e.PatientId == patientId &&
                schedule.Id == e.ReportScheduleId);

            // TODO: Return null if no entries found?

            Bundle bundle = CreateNewBundle();
            foreach (var entry in entries)
            {
                if (entry.MeasureReport == null) 
                {
                    continue;
                }

                MeasureReport mr = entry.MeasureReport;

                foreach(var r in entry.ContainedResources)
                {
                    if (r.DocumentId == null)
                        continue;

                    IFacilityResource facilityResource = null!;
                    
                    var resourceTypeCategory = ResourceCategory.GetResourceCategoryByType(r.ResourceType);

                    Resource resource = null;

                    try
                    {
                        if (resourceTypeCategory == ResourceCategoryType.Patient)
                        {
                            facilityResource = await _database.PatientResourceRepository.GetAsync(r.DocumentId);
                            resource = facilityResource.GetResource();
                            AddResourceToBundle(bundle, resource);
                        }
                        else
                        {
                            facilityResource = await _database.SharedResourceRepository.GetAsync(r.DocumentId);
                            resource = facilityResource.GetResource();
                            AddResourceToBundle(bundle, resource);
                        }
                    }
                    catch (Exception ex)
                    {
                        var message = "Contained resource could not be parsed into a valid Resource.";
                        _logger.LogError(ex, "{ResourceTypeName} with ID {ResourceId} contained resource could not be parsed into a valid Resource.", resource.TypeName, resource?.Id);

                        throw new Exception(message, ex);
                    }
                }
                

                // ensure we have an id to reference
                if (string.IsNullOrEmpty(mr.Id))
                    mr.Id = Guid.NewGuid().ToString();
                // ensure we have a meta object
                // set individual measure report profile
                mr.Meta = new Meta
                {
                    Profile = new List<string> { ReportConstants.BundleSettings.IndividualMeasureReportProfileUrl }
                };

                // clean up resource
                cleanupResource(mr);

                AddResourceToBundle(bundle, mr);

                _metrics.IncrementReportGeneratedCounter(new List<KeyValuePair<string, object?>>() {
                    new KeyValuePair<string, object?>("facilityId", schedule.FacilityId),
                    new KeyValuePair<string, object?>("measure.schedule.id", reportScheduleId),
                    new KeyValuePair<string, object?>("measure", mr.Measure)
                });
            }

            PatientSubmissionModel patientSubmissionModel = new PatientSubmissionModel()
            {
                FacilityId = facilityId,
                PatientId = patientId,
                ReportScheduleId = reportScheduleId,
                StartDate = schedule.ReportStartDate,
                EndDate = schedule.ReportEndDate,
                Bundle = bundle
            };

            return patientSubmissionModel;
        }

        #region Bundling Options

        private void cleanupResource(Resource resource)
        {
            if (resource is DomainResource)
            {
                DomainResource domainResource = (DomainResource)resource;
                
                // Remove extensions from resources
                domainResource.Extension.RemoveAll(e => e.Url != null && REMOVE_EXTENSIONS.Contains(e.Url));

                // Remove extensions from group/populations of MeasureReports
                if (resource is MeasureReport)
                {
                    MeasureReport measureReport = (MeasureReport)resource;
                    measureReport.Group.ForEach(g =>
                    {
                        g.Population.ForEach(p =>
                        {
                            p.Extension.RemoveAll(e => e.Url != null && REMOVE_EXTENSIONS.Contains(e.Url));
                        });
                    });
                    measureReport.EvaluatedResource.ForEach(er =>
                    {
                        er.Extension.RemoveAll(e => e.Url != null && REMOVE_EXTENSIONS.Contains(e.Url));

                    });

                }
            }
        }
        #endregion


        #region Common Methods

        protected Bundle CreateNewBundle()
        {
            Bundle bundle = new Bundle();
            bundle.Meta = new Meta
            {
                Profile = new string[] { ReportConstants.BundleSettings.ReportBundleProfileUrl },
                Tag = new List<Coding> { new Coding(ReportConstants.BundleSettings.MainSystem, "report", "Report") }
            };
            bundle.Identifier = new Identifier(ReportConstants.BundleSettings.IdentifierSystem, "urn:uuid:" + Guid.NewGuid());
            bundle.Type = Bundle.BundleType.Collection;
            bundle.Timestamp = DateTime.UtcNow;

            return bundle;
        }


        protected string GetRelativeReference(Resource resource)
        {
            return string.Format("{0}/{1}", resource.TypeName, resource.Id);
        }

        protected string GetFullUrl(Resource resource)
        {
            return string.Format(ReportConstants.BundleSettings.BundlingFullUrlFormat, GetRelativeReference(resource));
        }

        /// <summary>
        /// Adds the given resource to the given bundle, if not already present.
        /// </summary>
        /// <param name="bundle"></param>
        /// <param name="resource"></param>
        /// <returns></returns>
        protected Bundle.EntryComponent AddResourceToBundle(Bundle bundle, Resource resource)
        {
            var fullUrl = GetFullUrl(resource);
            var existingEntry = bundle.FindEntry(fullUrl).FirstOrDefault();
            return existingEntry ?? bundle.AddResourceEntry(resource, fullUrl);
        }

        #endregion

    }

}
