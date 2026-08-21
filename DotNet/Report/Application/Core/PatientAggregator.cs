using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Options;
using System.Text;

namespace LantanaGroup.Link.Report.Application.Core
{
    public class PatientAggregator
    {
        private readonly IReportServiceMetrics _metrics;
        private readonly IReportEntryManager _reportEntryManager;
        private readonly BlobStorageService _blobStorageService;
        private readonly BlobContainerClient _containerClient;
        private readonly BlobStorageSettings _settings;
        private readonly ITenantApiService _tenantApiService;
        private readonly PatientAggregatorSettings _aggregatorSettings;

        public PatientAggregator(
            IReportServiceMetrics metrics,
            IReportEntryManager reportEntryManager,
            BlobStorageService blobStorageService,
            IOptions<BlobStorageSettings> settings,
            ITenantApiService tenantApiService,
            IOptions<PatientAggregatorSettings> aggregatorSettings)
        {
            _metrics = metrics ?? throw new ArgumentException(nameof(metrics));
            _reportEntryManager = reportEntryManager;
            _blobStorageService = blobStorageService;
            _tenantApiService = tenantApiService;
            _aggregatorSettings = aggregatorSettings.Value;

            _settings = settings.Value;
            _containerClient = new BlobContainerClient(_settings.ConnectionString, _settings.BlobContainerName);
        }

        public async Task<AggregateResult> AggregateToABS(string patientId, ReportScheduleModel reportSchedule, CancellationToken cancellationToken = default)
        {
            if (_containerClient == null)
            {
                throw new Exception($"Blob Container Client could not be initialized when attempting to run patient aggregator (ReportId = {reportSchedule.Id}, PatientId = {patientId}, FacilityId = {reportSchedule.FacilityId}).");
            }

            AggregateResult aggregateResult = new AggregateResult();

            var entry = await _reportEntryManager.SingleOrDefaultAsync(x => x.ReportScheduleId == reportSchedule.Id && x.PatientId == patientId, cancellationToken);

            if (entry == null)
            {
                throw new Exception($"No ReportEntry record found when running patient aggregator (ReportId = {reportSchedule.Id}, PatientId = {patientId}, FacilityId = {reportSchedule.FacilityId}).");
            }

            //The 'resourcesAdded' HashSet will keep track of FHIR resource id's that have been added to the bundle to avoid adding duplicates across entries. 
            HashSet<string> resourcesAdded = new HashSet<string>();
            var parser = LinkFhirSerializerOptions.FhirJsonDeserializerPermissive;

            string bundleName = $"patient-{patientId}.ndjson";
            string reportName = _blobStorageService.GetReportName(reportSchedule);
            string blobName = _blobStorageService.GetBlobName(reportName, bundleName);

            AppendBlobClient writeBlobClient = _containerClient.GetAppendBlobClient(blobName);

            aggregateResult.Uri = writeBlobClient.Uri;
            aggregateResult.BlobName = blobName;

            using (Stream write_stream = await writeBlobClient.OpenWriteAsync(true, cancellationToken: cancellationToken))
            using (StreamWriter writer = new StreamWriter(write_stream))
            {
                foreach (var measureReportEntry in entry.MeasureReports.Where(x => x.Status == MeasureReportStatus.ReadyForValidation))
                {
                    BlockBlobClient readBlobClient = _containerClient.GetBlockBlobClient(measureReportEntry.MeasureReportFileName);
                    var aggregateMeasureReport = new AggregateMeasureReportResult() { ReportType = measureReportEntry.ReportType };

                    using (Stream read_stream = await readBlobClient.OpenReadAsync(cancellationToken: cancellationToken))
                    using (StreamReader reader = new StreamReader(read_stream))
                    {
                        while (reader.Peek() >= 0)
                        {
                            string resource_reference = reader.ReadLine();

                            if (string.IsNullOrWhiteSpace(resource_reference) || resourcesAdded.Contains(resource_reference))
                            {
                                //Skip FHIR Resource line
                                reader.ReadLine();
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

                            aggregateMeasureReport.ResourceReferences.Add(split_reference);

                            if (split_reference[0] != "MeasureReport")
                            {
                                resourcesAdded.Add(resource_reference);
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
                            resourcesAdded.Add(resource_reference);
                            writer.WriteLine(measureReportString);

                            _metrics.IncrementReportGeneratedCounter(new List<KeyValuePair<string, object?>>() {
                                new KeyValuePair<string, object?>("facilityId", entry.FacilityId),
                                new KeyValuePair<string, object?>("measure.schedule.id", reportSchedule.Id),
                                new KeyValuePair<string, object?>("measure", measureReport.Measure)
                            });
                        }
                    }
                }

                if (_aggregatorSettings.IncludeOrganizationResource)
                {
                    var facilityConfig = await _tenantApiService.GetFacilityConfig(reportSchedule.FacilityId);
                    if (facilityConfig != null)
                    {
                        var organization = FhirHelperMethods.CreateOrganization(
                            facilityConfig.FacilityName,
                            reportSchedule.FacilityId,
                            ReportConstants.BundleSettings.SubmittingOrganizationProfile,
                            ReportConstants.BundleSettings.OrganizationTypeSystem,
                            ReportConstants.BundleSettings.CdcOrgIdSystem,
                            ReportConstants.BundleSettings.DataAbsentReasonExtensionUrl,
                            ReportConstants.BundleSettings.DataAbsentReasonUnknownCode);
                        var serializer = new FhirJsonSerializer();
                        writer.WriteLine(serializer.SerializeToString(organization));
                    }
                }
            }

            return aggregateResult;
        }

        public virtual async System.Threading.Tasks.Task AppendResourceToBlob(string uri, DomainResource domainResource)
        {
            AppendBlobClient appendBlobClient = _containerClient.GetAppendBlobClient(uri);

            var serializer = new FhirJsonSerializer();
            string resourceString = serializer.SerializeToString(domainResource);

            byte[] string_bytes = Encoding.UTF8.GetBytes(resourceString + Environment.NewLine);

            using (var stream = new MemoryStream(string_bytes))
            {
                await appendBlobClient.AppendBlockAsync(stream);
            }
        }
    }
}