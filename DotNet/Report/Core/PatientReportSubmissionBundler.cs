using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Google.Protobuf.WellKnownTypes;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Application.ResourceCategories;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.Options;
using System.Text;
using System.Threading;

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
        private readonly BlobStorageService _blobStorageService;
        private readonly BlobContainerClient _containerClient;
        private readonly BlobStorageSettings _settings;

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

        public PatientReportSubmissionBundler(ILogger<PatientReportSubmissionBundler> logger, IDatabase database, IReportServiceMetrics metrics, IReportScheduledManager reportScheduledManager, BlobStorageService blobStorageService, IOptions<BlobStorageSettings> settings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metrics = metrics ?? throw new ArgumentException(nameof(metrics));
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _reportScheduledManager = reportScheduledManager ?? throw new ArgumentNullException(nameof(reportScheduledManager));
            _blobStorageService = blobStorageService;

            _settings = settings.Value;
            if (_settings.ConnectionString != null)
            {
                _containerClient = new BlobContainerClient(_settings.ConnectionString, _settings.BlobContainerName);
            }
        }


        public async Task<PatientSubmissionModel> GenerateBundle(string facilityId, string patientId, string reportScheduleId)
        {
            var schedule = await _reportScheduledManager.SingleOrDefaultAsync(s => s.Id == reportScheduleId) ?? throw new Exception($"No Measure Reports Scheduled for reportScheduleId of {reportScheduleId}");

            var entries = await _database.SubmissionEntryRepository.FindAsync(e =>
                e.FacilityId == facilityId && e.PatientId == patientId &&
                schedule.Id == e.ReportScheduleId);

            //The 'resourcesAdded' Dictionary will keep track of FHIR resource id's that have been added to the bundle to avoid adding duplicates across entries. The value of each dictionary entry will contain the associated FHIR types. It's a string List type in case there are different FHIR resources that share the same id. This is probably unlikely to happen, but is possible. 
            Dictionary<string, List<string>> resourcesAdded = new Dictionary<string, List<string>>();
            Bundle bundle = CreateNewBundle();

            foreach (var entry in entries)
            {
                if (entry.MeasureReport == null) 
                {
                    continue;
                }

                MeasureReport mr = entry.MeasureReport;

                foreach (var r in entry.ContainedResources)
                {
                    if (r.DocumentId == null)
                    {
                        //TODO: Log if this happens?
                        continue;
                    }

                    if (resourcesAdded.ContainsKey(r.ResourceId) && resourcesAdded[r.ResourceId].Where(x => x == r.ResourceType).Any())
                    {
                        continue;
                    }

                    IFacilityResource facilityResource = null!;
                    
                    var resourceTypeCategory = ResourceCategory.GetResourceCategoryByType(r.ResourceType);

                    try
                    {
                        if (resourceTypeCategory == ResourceCategoryType.Patient)
                        {
                            facilityResource = await _database.PatientResourceRepository.GetAsync(r.DocumentId);
                            AddResourceToBundle(bundle, facilityResource.GetResource());
                        }
                        else
                        {
                            facilityResource = await _database.SharedResourceRepository.GetAsync(r.DocumentId);
                            AddResourceToBundle(bundle, facilityResource.GetResource());
                        }

                        if (resourcesAdded.ContainsKey(r.ResourceId))
                        {
                            resourcesAdded[r.ResourceId].Add(r.ResourceType);
                        }
                        else
                        {
                            resourcesAdded.Add(r.ResourceId, new List<string>() { r.ResourceType });
                        }
                    }
                    catch (Exception ex)
                    {
                        var message = "Contained resource could not be parsed into a valid Resource.";
                        _logger.LogError(ex, "{ResourceTypeName} with ID {ResourceId} contained resource could not be parsed into a valid Resource.", r.ResourceType, r.ResourceId);

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

        public async Task<Uri> GenerateBundleToABS(string patientId, string reportScheduleId)
        {
            var entries = (await _database.ReportEntryStatusRepository.FindAsync(x => x.ReportScheduleId == reportScheduleId && x.PatientId == patientId)).ToList();

            //TODO: Add missing entry check

            //The 'resourcesAdded' Dictionary will keep track of FHIR resource id's that have been added to the bundle to avoid adding duplicates across entries. The value of each dictionary entry will contain the associated FHIR types. It's a string List type in case there are different FHIR resources that share the same id. This is probably unlikely to happen, but is possible. 
            Dictionary<string, int> resourcesAdded = new Dictionary<string,int>();

            BlockBlobClient blockWriteBlobClient = _containerClient.GetBlockBlobClient("Patient_" + patientId + ".ndjson");

            using (Stream write_stream = await blockWriteBlobClient.OpenWriteAsync(true))
            using (StreamWriter writer = new StreamWriter(write_stream))
            {
                foreach (var entry in entries)
                {
                    BlockBlobClient blockReadBlobClient = _containerClient.GetBlockBlobClient(entry.MeasureReportFileName);
                    
                    try
                    {
                        using (Stream read_stream = await blockReadBlobClient.OpenReadAsync(true))
                        using (StreamReader reader = new StreamReader(read_stream))
                        {
                            while (reader.Peek() >= 0)
                            {
                                string resource_and_id = reader.ReadLine();

                                if (resourcesAdded.ContainsKey(resource_and_id))
                                {
                                    //Skip FHIR Resource line
                                    reader.Read();
                                    continue;
                                }

                                resourcesAdded.Add(resource_and_id, 1);
                                writer.WriteLine(reader.ReadLine());
                            }
                        }
                    }
                    catch (Exception ex) {
                        //TODO: Do something with this catch
                        throw ex;
                    }
                }
            }

            return blockWriteBlobClient.Uri;
        }

        public async Task<bool> GenerateBundleFromABS()
        {
            //The 'resourcesAdded' Dictionary will keep track of FHIR resource id's that have been added to the bundle to avoid adding duplicates across entries. The value of each dictionary entry will contain the associated FHIR types. It's a string List type in case there are different FHIR resources that share the same id. This is probably unlikely to happen, but is possible. 
            Dictionary<string, List<string>> resourcesAdded = new Dictionary<string, List<string>>();
            BlockBlobClient blockReadBlobClient = _containerClient.GetBlockBlobClient("Patient_3f993147-8cd4-44c6-bbb5-9f25fe428517");
            BlockBlobClient blockWriteBlobClient = _containerClient.GetBlockBlobClient("Patient_3f993147-8cd4-44c6-bbb5-9f25fe428517.ndjson");

            using (Stream read_stream = await blockReadBlobClient.OpenReadAsync(true))
            using (Stream write_stream = await blockWriteBlobClient.OpenWriteAsync(true))
            using (StreamReader reader = new StreamReader(read_stream)) 
            using (StreamWriter writer = new StreamWriter(write_stream))
                
            while (reader.Peek() >= 0)
            {
                string[] resource_and_id = reader.ReadLine().Split("_");

                if (resourcesAdded.ContainsKey(resource_and_id[1]) && resourcesAdded[resource_and_id[1]].Where(x => x == resource_and_id[0]).Any())
                {
                    //Skip resource line
                    reader.Read();
                    continue;
                }

                if (resourcesAdded.ContainsKey(resource_and_id[1]))
                {
                    resourcesAdded[resource_and_id[1]].Add(resource_and_id[0]);
                }
                else
                {
                    resourcesAdded.Add(resource_and_id[1], new List<string>() { resource_and_id[0] });
                }

                writer.WriteLine(reader.ReadLine());        
            }


            return true;
        }

        public async Task<bool> GenerateBundleFromABSMulti()
        {
            //The 'resourcesAdded' Dictionary will keep track of FHIR resource id's that have been added to the bundle to avoid adding duplicates across entries. The value of each dictionary entry will contain the associated FHIR types. It's a string List type in case there are different FHIR resources that share the same id. This is probably unlikely to happen, but is possible. 
            Dictionary<string, List<string>> resourcesAdded = new Dictionary<string, List<string>>();
            
            BlockBlobClient blockWriteBlobClient = _containerClient.GetBlockBlobClient("Patient_3f993147-8cd4-44c6-bbb5-9f25fe428517.ndjson");

            using (Stream write_stream = await blockWriteBlobClient.OpenWriteAsync(true))
            using (StreamWriter writer = new StreamWriter(write_stream))

                for (int i = 0; i < 2; i++) {
                    BlockBlobClient blockReadBlobClient = _containerClient.GetBlockBlobClient("Patient_3f993147-8cd4-44c6-bbb5-9f25fe428517_multi_" + (i + 1));
                    using (Stream read_stream = await blockReadBlobClient.OpenReadAsync(true))
                    using (StreamReader reader = new StreamReader(read_stream))

                        while (reader.Peek() >= 0)
                        {
                            string[] resource_and_id = reader.ReadLine().Split("_");

                            if (resourcesAdded.ContainsKey(resource_and_id[1]) && resourcesAdded[resource_and_id[1]].Where(x => x == resource_and_id[0]).Any())
                            {
                                //Skip resource line
                                reader.Read();
                                continue;
                            }

                            if (resourcesAdded.ContainsKey(resource_and_id[1]))
                            {
                                resourcesAdded[resource_and_id[1]].Add(resource_and_id[0]);
                            }
                            else
                            {
                                resourcesAdded.Add(resource_and_id[1], new List<string>() { resource_and_id[0] });
                            }

                            writer.WriteLine(reader.ReadLine());
                        }
                }

            return true;
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
        protected void AddResourceToBundle(Bundle bundle, Resource resource)
        {
            var fullUrl = GetFullUrl(resource);

            bundle.AddResourceEntry(resource, fullUrl);
        }

        #endregion
    }
}
