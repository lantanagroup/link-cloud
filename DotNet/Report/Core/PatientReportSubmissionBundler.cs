using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Google.Protobuf.WellKnownTypes;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Application.ResourceCategories;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.Options;
using System;
using System.Text;
using System.Threading;
using static System.Net.Mime.MediaTypeNames;

//TODO: Add story for extension removal Normalization
//TODO: Add story for MeasureEval post eval extension removal (set in app config)
//TODO: Add story for MeasureEval to add Meta.Profile to each measure report: "http://hl7.org/fhir/us/davinci-deqm/StructureDefinition/indv-measurereport-deqm";

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

        public async Task<AggregateResult> GenerateBundleToABS(string patientId, string reportScheduleId)
        {
            AggregateResult aggregateResult = new AggregateResult();

            var entry = (await _database.ReportEntryStatusRepository.SingleOrDefaultAsync(x => x.ReportScheduleId == reportScheduleId && x.PatientId == patientId));

            //TODO: Add missing entry check

            //The 'resourcesAdded' Dictionary will keep track of FHIR resource id's that have been added to the bundle to avoid adding duplicates across entries. The value of each dictionary entry will contain the associated FHIR types. It's a string List type in case there are different FHIR resources that share the same id. This is probably unlikely to happen, but is possible. 
            Dictionary<string, int> resourcesAdded = new Dictionary<string,int>();
            var parser = new FhirJsonParser();

            //TODO: Need to get the actual source file
            AppendBlobClient blockWriteBlobClient = _containerClient.GetAppendBlobClient("Patient_" + patientId + ".ndjson");
            aggregateResult.Uri = blockWriteBlobClient.Uri;

            using (Stream write_stream = await blockWriteBlobClient.OpenWriteAsync(true))
            using (StreamWriter writer = new StreamWriter(write_stream))
            {
                foreach (var measureReportEntry in entry.MeasureReportEntryList)
                {
                    BlockBlobClient blockReadBlobClient = _containerClient.GetBlockBlobClient(measureReportEntry.MeasureReportFileName);

                    try
                    {
                        using (Stream read_stream = await blockReadBlobClient.OpenReadAsync(true))
                        using (StreamReader reader = new StreamReader(read_stream))
                        {
                            while (reader.Peek() >= 0)
                            {
                                string resource_and_id = reader.ReadLine();

                                if (string.IsNullOrWhiteSpace(resource_and_id) || resourcesAdded.ContainsKey(resource_and_id))
                                {
                                    //Skip FHIR Resource line
                                    reader.Read();
                                    continue;
                                }

                                //TODO: Change to '/'
                                if (resource_and_id.Split('_')[0] == "MeasureReport")
                                {
                                    string measureReportString = reader.ReadLine();
                                    MeasureReport measureReport = parser.Parse<MeasureReport>(measureReportString);

                                    var aggregateMeasureReport = new AggregateMeasureReportResult() { Measure = measureReport.Measure, MeasureReportId = measureReport.Id };

                                    foreach (var group in measureReport.Group) {
                                        foreach (var population in group.Population) {
                                            aggregateMeasureReport.PopulationList.Add(new AggregateMeasureReportPopulation()
                                            {
                                                PopulationCode = population.Code,
                                                PopulationCount = population.Count ?? 0,
                                                PopulationId = population.ElementId
                                            });
                                        }
                                    }

                                    aggregateResult.MeasureReportResults.Add(aggregateMeasureReport);
                                    resourcesAdded.Add(resource_and_id, 1);
                                    writer.WriteLine(measureReportString);
                                }
                                else {
                                    resourcesAdded.Add(resource_and_id, 1);
                                    writer.WriteLine(reader.ReadLine());
                                }
                            }
                        }

                        //TODO: Is this a helpful metric to capture as is?
                        _metrics.IncrementReportGeneratedCounter(new List<KeyValuePair<string, object?>>() {
                            new KeyValuePair<string, object?>("facilityId", entry.FacilityId),
                            new KeyValuePair<string, object?>("measure.schedule.id", reportScheduleId),
                            //new KeyValuePair<string, object?>("measure", mr.Measure)
                        });
                    }
                    catch (Exception ex) {
                        //TODO: Do something with this catch
                        throw ex;
                    }
                }
            }

            return aggregateResult;
        }

        public async void AppendToBlob(string uri, DomainResource domainResource)  
        {
            AppendBlobClient appendBlobClient = _containerClient.GetAppendBlobClient(uri);

            var serializer = new FhirJsonSerializer();
            string resourceString = serializer.SerializeToString(domainResource);

            byte[] string_bytes = Encoding.UTF8.GetBytes(resourceString);

            using (var stream = new MemoryStream(string_bytes))
            {
                await appendBlobClient.AppendBlockAsync(stream);
            }
        }
    }
}
