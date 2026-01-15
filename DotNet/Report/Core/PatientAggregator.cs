using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Google.Protobuf.WellKnownTypes;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using System;
using System.Text;
using System.Threading;
using static System.Net.Mime.MediaTypeNames;

namespace LantanaGroup.Link.Report.Core
{
    public class PatientAggregator
    {
        private readonly ILogger<PatientAggregator> _logger;
        private readonly IReportServiceMetrics _metrics;
        private readonly IDatabase _database;
        private readonly IReportScheduledManager _reportScheduledManager;
        private readonly BlobStorageService _blobStorageService;
        private readonly BlobContainerClient _containerClient;
        private readonly BlobStorageSettings _settings;

        public PatientAggregator(ILogger<PatientAggregator> logger, IDatabase database, IReportServiceMetrics metrics, IReportScheduledManager reportScheduledManager, BlobStorageService blobStorageService, IOptions<BlobStorageSettings> settings)
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

        public async Task<AggregateResult> AggregateToABS(string patientId, ReportSchedule reportSchedule)
        {
            if (_containerClient == null) 
            {
                throw new Exception($"Blob Container Client could not be initialized when attempting to run patient aggregator (PatientId = {patientId}, FacilityId = {reportSchedule.FacilityId}).");    
            }

            AggregateResult aggregateResult = new AggregateResult();

            var entry = (await _database.ReportEntryRepository.SingleOrDefaultAsync(x => x.ReportScheduleId == reportSchedule.Id && x.PatientId == patientId));

            if (entry == null)
            {
                throw new Exception($"No ReportEntry record found when running patient aggregator (PatientId = {patientId}, FacilityId = {reportSchedule.FacilityId}).");
            }

            //The 'resourcesAdded' Dictionary will keep track of FHIR resource id's that have been added to the bundle to avoid adding duplicates across entries. The value of each dictionary entry will contain the associated FHIR types. It's a string List type in case there are different FHIR resources that share the same id. This is probably unlikely to happen, but is possible. 
            Dictionary<string, int> resourcesAdded = new Dictionary<string, int>();
            var parser = new FhirJsonParser();

            string bundleName = $"patient-{patientId}.ndjson";
            string reportName = _blobStorageService.GetReportName(reportSchedule);
            string blobName = _blobStorageService.GetBlobName(reportName, bundleName);

            AppendBlobClient blockWriteBlobClient = _containerClient.GetAppendBlobClient(blobName);

            aggregateResult.Uri = blockWriteBlobClient.Uri;
            aggregateResult.BlobName = blobName;

            using (Stream write_stream = await blockWriteBlobClient.OpenWriteAsync(true))
            using (StreamWriter writer = new StreamWriter(write_stream))
            {
                foreach (var measureReportEntry in entry.MeasureReportList)
                {
                    BlockBlobClient blockReadBlobClient = _containerClient.GetBlockBlobClient(measureReportEntry.MeasureReportFileName);
                    var aggregateMeasureReport = new AggregateMeasureReportResult() { ReportType = measureReportEntry.ReportType };

                    using (Stream read_stream = await blockReadBlobClient.OpenReadAsync(true))
                    using (StreamReader reader = new StreamReader(read_stream))
                    {
                        while (reader.Peek() >= 0)
                        {
                            string resource_reference = reader.ReadLine();

                            if (string.IsNullOrWhiteSpace(resource_reference) || resourcesAdded.ContainsKey(resource_reference))
                            {
                                //Skip FHIR Resource line
                                reader.Read();
                                continue;
                            }

                            //TODO: Test for malformed references
                            string[] split_reference = resource_reference.Split('/');

                            if (aggregateMeasureReport.ResourceCount.ContainsKey(split_reference[0]))
                            {
                                aggregateMeasureReport.ResourceCount[split_reference[0]]++;
                            }
                            else
                            {
                                aggregateMeasureReport.ResourceCount.Add(split_reference[0], 1);
                            }

                            aggregateMeasureReport.Resources.Add(split_reference);

                            if (split_reference[0] != "MeasureReport")
                            {
                                resourcesAdded.Add(resource_reference, 1);
                                writer.WriteLine(reader.ReadLine());
                                continue;
                            }

                            string measureReportString = reader.ReadLine();
                            MeasureReport measureReport = parser.Parse<MeasureReport>(measureReportString);

                            aggregateMeasureReport.Measure = measureReport.Measure;
                            aggregateMeasureReport.MeasureReportId = measureReport.Id;

                            foreach (var group in measureReport.Group)
                            {
                                foreach (var population in group.Population)
                                {
                                    aggregateMeasureReport.PopulationList.Add(new AggregateMeasureReportPopulation()
                                    {
                                        PopulationCode = population.Code,
                                        PopulationCount = population.Count ?? 0,
                                        PopulationId = population.ElementId
                                    });
                                }
                            }

                            aggregateResult.MeasureReportResults.Add(aggregateMeasureReport);
                            resourcesAdded.Add(resource_reference, 1);
                            writer.WriteLine(measureReportString);

                            _metrics.IncrementReportGeneratedCounter(new List<KeyValuePair<string, object?>>() {
                                new KeyValuePair<string, object?>("facilityId", entry.FacilityId),
                                new KeyValuePair<string, object?>("measure.schedule.id", reportSchedule.Id),
                                new KeyValuePair<string, object?>("measure", measureReport.Measure)
                            });
                        }
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