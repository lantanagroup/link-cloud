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

        public async Task<Uri> GenerateBundleToABS(string patientId, string reportScheduleId)
        {
            var entries = (await _database.ReportEntryStatusRepository.FindAsync(x => x.ReportScheduleId == reportScheduleId && x.PatientId == patientId)).ToList();

            //TODO: Add missing entry check

            //The 'resourcesAdded' Dictionary will keep track of FHIR resource id's that have been added to the bundle to avoid adding duplicates across entries. The value of each dictionary entry will contain the associated FHIR types. It's a string List type in case there are different FHIR resources that share the same id. This is probably unlikely to happen, but is possible. 
            Dictionary<string, int> resourcesAdded = new Dictionary<string,int>();

            //TODO: Need to get the actual source file
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

            return blockWriteBlobClient.Uri;
        }

        public async void AddLineToExistingABSBlob(ReportEntryStatusModel entry, string resourceString, string resourceReference)  
        {
            //TODO: Look if initial file should be a block or append blob
            //TODO: Need to get the actual source file
            AppendBlobClient appendBlobClient = _containerClient.GetAppendBlobClient("Patient_" + entry.PatientId + ".ndjson");

            StringBuilder sb = new StringBuilder();
            sb.Append(Environment.NewLine);
            sb.Append(resourceReference);
            sb.Append(Environment.NewLine);
            sb.Append(resourceString);

            byte[] string_bytes = Encoding.UTF8.GetBytes(sb.ToString());

            using (var stream = new MemoryStream(string_bytes))
            {
                await appendBlobClient.AppendBlockAsync(stream);
            }
        }
    }
}
