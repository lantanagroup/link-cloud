using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Queries;
using LantanaGroup.Link.Report.Entities;
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
        private readonly IServiceScopeFactory _serviceScopeFactory;

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

        public PatientReportSubmissionBundler(ILogger<PatientReportSubmissionBundler> logger, IServiceScopeFactory serviceScopeFactory, IReportServiceMetrics metrics)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metrics = metrics ?? throw new ArgumentException(nameof(metrics));
        }
        public async Task<PatientSubmissionModel> GenerateBundle(string facilityId, string patientId, string reportScheduleId)
        {
            var queries = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<ISubmissionEntryQueries>();
            var patientReportData = await queries.GetPatientReportData(facilityId, reportScheduleId, patientId, cancellationToken: CancellationToken.None);
            return await GenerateBundle(patientReportData, facilityId, patientId, reportScheduleId);
        }

        public async Task<PatientSubmissionModel> GenerateBundle(PatientReportData patientReportData, string facilityId, string patientId, string reportScheduleId)
        {
            var schedule = patientReportData.Schedule;

            //The 'resourcesAdded' Dictionary will keep track of FHIR resource id's that have been added to the bundle to avoid adding duplicates across entries. The value of each dictionary entry will contain the associated FHIR types. It's a string List type in case there are different FHIR resources that share the same id. This is probably unlikely to happen, but is possible. 
            Dictionary<string, List<string>> resourcesAdded = new Dictionary<string, List<string>>();

            Bundle bundle = CreateNewBundle();
            foreach (var reportType in patientReportData.ReportData)
            {
                var report = reportType.Key;
                var data = reportType.Value;

                foreach (var fhirResource in data.Resources)
                {
                    if (fhirResource.Id == null)
                        continue;

                    if (resourcesAdded.ContainsKey(fhirResource.ResourceId) && resourcesAdded[fhirResource.ResourceId].Contains(fhirResource.ResourceType))
                    {
                        continue;
                    }

                    var fullUrl = GetFullUrl(fhirResource.Resource);
                    bundle.AddResourceEntry(fhirResource.Resource, fullUrl);

                    if (resourcesAdded.ContainsKey(fhirResource.ResourceId))
                    {
                        resourcesAdded[fhirResource.ResourceId].Add(fhirResource.ResourceType);
                    }
                    else
                    {
                        resourcesAdded.Add(fhirResource.ResourceId, new List<string>() { fhirResource.ResourceType });
                    }
                }

                foreach (var entry in data.Entries)
                {
                    if (entry.MeasureReport == null)
                    {
                        continue;
                    }

                    MeasureReport mr = entry.MeasureReport;

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

                    var fullUrl = GetFullUrl(mr);
                    bundle.AddResourceEntry(mr, fullUrl);

                    _metrics.IncrementReportGeneratedCounter(new List<KeyValuePair<string, object?>>() {
                    new KeyValuePair<string, object?>("facilityId", schedule.FacilityId),
                    new KeyValuePair<string, object?>("measure.schedule.id", reportScheduleId),
                    new KeyValuePair<string, object?>("measure", mr.Measure)
                });
                }
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
        #endregion

    }

}