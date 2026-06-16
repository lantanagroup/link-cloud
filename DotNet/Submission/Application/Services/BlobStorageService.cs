using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Submission.Application.Config;
using LantanaGroup.Link.Submission.Application.Interfaces;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Submission.Application.Services
{
    public class BlobStorageService : IStorageService
    {
        private readonly ILogger<BlobStorageService> _logger;
        private readonly InternalBlobStorageSettings _internalSettings;
        private readonly ExternalBlobStorageSettings _externalSettings;
        private readonly BlobContainerClient? _internalContainerClient;
        private readonly BlobContainerClient? _externalContainerClient;
        private readonly bool _useNdJson;

        public string DestinationType => "azure_blob_storage";

        private static BlobContainerClient? GetContainerClient(BlobStorageSettings settings)
        {
            if (settings.ConnectionString == null)
            {
                return null;
            }
            return new BlobContainerClient(settings.ConnectionString, settings.BlobContainerName);
        }

        private static string GetBlobName(string? blobRoot, params string[] segments)
        {
            IEnumerable<string> enumerable = segments;
            if (!string.IsNullOrEmpty(blobRoot))
            {
                enumerable = enumerable.Prepend(blobRoot);
            }
            return string.Join('/', enumerable.Select(component => component.Trim('/')));
        }

        public BlobStorageService(
            ILogger<BlobStorageService> logger,
            IOptions<InternalBlobStorageSettings> internalSettings,
            IOptions<ExternalBlobStorageSettings> externalSettings,
            IConfiguration configuration)
        {
            _logger = logger;
            _internalSettings = internalSettings.Value;
            _externalSettings = externalSettings.Value;
            _internalContainerClient = GetContainerClient(_internalSettings);
            _externalContainerClient = GetContainerClient(_externalSettings);
            _useNdJson = configuration.GetValue<bool>("useNdJson");
        }

        private string ChangeBlobRoot(string blobName)
        {
            if (_internalSettings.BlobRoot != null && blobName.StartsWith(_internalSettings.BlobRoot))
            {
                blobName = blobName.Substring(_internalSettings.BlobRoot.Length);
            }
            return GetBlobName(_externalSettings.BlobRoot, blobName);
        }

        public bool HasInternalClient()
        {
            return _internalContainerClient != null;
        }

        public async Task<byte[]?> DownloadFromInternalAsync(
            SubmitPayloadValue value,
            CancellationToken cancellationToken = default)
        {
            if (!HasInternalClient())
            {
                throw new InvalidOperationException("Not configured for internal blob storage.");
            }
            if (string.IsNullOrEmpty(value.PayloadUri))
            {
                return null;
            }
            BlobUriBuilder uriBuilder = new(new Uri(value.PayloadUri));
            // TODO: Check account/container name for consistency with _internalContainerClient?
            _logger.LogDebug("Downloading: {}", uriBuilder.BlobName);
            BlockBlobClient blobClient = _internalContainerClient.GetBlockBlobClient(uriBuilder.BlobName);
            using Stream input = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
            using MemoryStream output = new();
            await input.CopyToAsync(output, cancellationToken);
            _logger.LogDebug("Downloaded: {} byte(s)", output.Length);
            return output.ToArray();
        }

        public bool HasExternalClient()
        {
            return _externalContainerClient != null;
        }

        public async Task UploadToExternalAsync(
            SubmitPayloadKey key,
            SubmitPayloadValue value,
            byte[] content,
            CancellationToken cancellationToken = default)
        {
            if (!HasExternalClient())
            {
                throw new InvalidOperationException("Not configured for external blob storage.");
            }

            // When not using ndjson, we process everything during the manifest upload
            if (!_useNdJson)
            {
                if (value.PayloadType == PayloadType.MeasureReportSubmissionEntry)
                {
                    // Skip uploading individual patient files to external storage
                    // They will be processed when the manifest is uploaded
                    _logger.LogDebug("Skipping external upload for patient file (will be processed with manifest)");
                    return;
                }
                else if (value.PayloadType == PayloadType.ReportSchedule)
                {
                    await ProcessAndUploadExpandedBundlesAsync(key, value, content, cancellationToken);
                    return;
                }
            }

            // Original ndjson flow
            string blobName;
            if (!string.IsNullOrEmpty(value.PayloadUri))
            {
                BlobUriBuilder uriBuilder = new(new Uri(value.PayloadUri));
                blobName = ChangeBlobRoot(uriBuilder.BlobName);
            }
            else
            {
                string reportName = ReportHelpers.GetReportName(key.ReportScheduleId, key.FacilityId, value.ReportTypes, value.StartDate);
                string bundleName = value.PayloadType switch
                {
                    PayloadType.MeasureReportSubmissionEntry => $"patient-{value.PatientId}.ndjson",
                    PayloadType.ReportSchedule => "manifest.ndjson",
                    _ => $"{Guid.NewGuid()}.ndjson"
                };
                blobName = GetBlobName(_externalSettings.BlobRoot, reportName, bundleName);
            }

            _logger.LogDebug("Uploading: {}", blobName);
            BlockBlobClient blobClient = _externalContainerClient.GetBlockBlobClient(blobName);
            BlockBlobOpenWriteOptions blobOptions = new()
            {
                HttpHeaders = new()
                {
                    ContentType = "application/fhir+ndjson"
                }
            };

            using Stream stream = await blobClient.OpenWriteAsync(true, blobOptions, cancellationToken);
            await stream.WriteAsync(content, cancellationToken);
        }

        private async Task ProcessAndUploadExpandedBundlesAsync(
            SubmitPayloadKey key,
            SubmitPayloadValue value,
            byte[] content,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Processing manifest for expanded bundles - ReportScheduleId: {ReportScheduleId}", key.ReportScheduleId);

            // Parse manifest NDJSON
            string manifestContent = Encoding.UTF8.GetString(content);
            var jsonOptions = new JsonSerializerOptions().ForFhir(new FhirJsonPocoDeserializerSettings { Validator = null });
            List<Resource> manifestResources = new();

            var lines = manifestContent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var line in lines)
            {
                try
                {
                    var patchedLine = PatchEmptyDeviceVersionValue(line);
                    var resource = JsonSerializer.Deserialize<Resource>(patchedLine, jsonOptions);
                    if (resource != null)
                    {
                        manifestResources.Add(resource);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize manifest line. Line preview: {LinePreview}",
                        line.Length > 200 ? line.Substring(0, 200) + "..." : line);
                }
            }

            _logger.LogInformation("Successfully parsed {Count} resources from manifest", manifestResources.Count);

            // Extract specific resource types from manifest
            var organization = manifestResources.OfType<Organization>().FirstOrDefault();
            var device = manifestResources.OfType<Device>().FirstOrDefault();
            var censusList = manifestResources.OfType<Hl7.Fhir.Model.List>().FirstOrDefault();
            var aggregateReports = manifestResources.OfType<MeasureReport>().Where(IsAggregateMeasureReport).ToList();
            var operationOutcome = manifestResources.OfType<OperationOutcome>().FirstOrDefault();

            _logger.LogInformation("Found {Count} aggregate MeasureReports", aggregateReports.Count);

            if (aggregateReports.Any())
            {
                foreach (var agg in aggregateReports)
                {
                    _logger.LogDebug("Aggregate report parsed: ID={Id}, Type={Type}, Measure={Measure}",
                        agg.Id ?? "no-id", agg.Type?.ToString() ?? "null", agg.Measure ?? "no-measure");
                }
            }
            else
            {
                _logger.LogWarning("No aggregate reports detected in manifest after parsing.");
            }

            string measureFolder = GetMeasureFolderPath(value.ReportTypes);
            string reportName = ReportHelpers.GetReportName(key.ReportScheduleId, key.FacilityId, value.ReportTypes, value.StartDate);

            string? nhsnOrgId = organization?.Identifier
                .FirstOrDefault(i => i.System == "https://www.cdc.gov/nhsn/OrgID")?.Value;
            nhsnOrgId ??= key.FacilityId;

            if (string.IsNullOrEmpty(value.PayloadUri))
            {
                _logger.LogError("PayloadUri is null or empty for ReportSchedule - cannot process expanded bundles");
                return;
            }

            // Derive root prefix from value.PayloadUri
            BlobUriBuilder uriBuilder = new(new Uri(value.PayloadUri!));
            string manifestBlobName = uriBuilder.BlobName;
            int lastSlash = manifestBlobName.LastIndexOf('/');
            string rootPrefix = lastSlash >= 0 ? manifestBlobName.Substring(0, lastSlash + 1) : "";

            _logger.LogDebug("Downloading files under prefix: {Prefix}", rootPrefix);

            // Download all files under the root prefix once
            var allFiles = await DownloadAsync(_internalContainerClient!, rootPrefix, cancellationToken);

            _logger.LogInformation("Downloaded {FileCount} files from internal storage", allFiles.Count);
            if (allFiles.Count > 0)
            {
                _logger.LogDebug("Available files: {Files}", string.Join(", ", allFiles.Keys));
            }

            // Find patient IDs from the List resource in manifest
            var patientIds = new List<string>();
            if (censusList != null)
            {
                foreach (var entry in censusList.Entry ?? new List<Hl7.Fhir.Model.List.EntryComponent>())
                {
                    var refId = entry.Item?.Reference?.Split('/').Last();
                    if (!string.IsNullOrEmpty(refId))
                    {
                        patientIds.Add(refId);
                    }
                }
            }
            else
            {
                _logger.LogWarning("No patient List found in manifest. Falling back to file names.");
            }

            // Fallback: discover from file names
            var idsFromFiles = allFiles.Keys
                .Where(k => k.StartsWith("patient-") && k.EndsWith(".ndjson"))
                .Select(k => k.Replace("patient-", "").Replace(".ndjson", ""))
                .ToList();

            patientIds.AddRange(idsFromFiles.Except(patientIds));
            patientIds = patientIds.Distinct().ToList();

            _logger.LogInformation("Discovered {Count} unique patient IDs for bundle creation: {Ids}", patientIds.Count, string.Join(", ", patientIds));

            string startDateStr = value.StartDate?.ToString("yyyyMMdd") ?? "unknown";
            string measureAcronym = GetMeasureAcronym(value.ReportTypes);

            // Process each patient
            foreach (var patientId in patientIds)
            {
                string patientFileName = $"patient-{patientId}.ndjson";
                if (!allFiles.TryGetValue(patientFileName, out var patientBytes) || patientBytes == null)
                {
                    _logger.LogWarning("Missing NDJSON file for patient {PatientId}", patientId);
                    continue;
                }

                var patientResources = new List<Resource>();
                string patientContent = Encoding.UTF8.GetString(patientBytes);
                foreach (var line in patientContent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    try
                    {
                        var patchedLine = PatchEmptyDeviceVersionValue(line);
                        var res = JsonSerializer.Deserialize<Resource>(patchedLine, jsonOptions);
                        if (res != null) patientResources.Add(res);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse line in patient {PatientId} NDJSON", patientId);
                    }
                }

                _logger.LogDebug("Parsed {Count} resources for patient {PatientId}", patientResources.Count, patientId);

                // Find the individual MeasureReport in patient resources
                var patientMR = patientResources.OfType<MeasureReport>()
                    .FirstOrDefault(mr => !IsAggregateMeasureReport(mr) && mr.Subject?.Reference?.Contains(patientId) == true);

                if (patientMR == null)
                {
                    _logger.LogWarning("No individual MeasureReport found in NDJSON for patient {PatientId} - skipping bundle", patientId);
                    continue;
                }

                _logger.LogInformation("Found individual MeasureReport ID {MRId} for patient {PatientId}", patientMR.Id ?? "no-id", patientId);

                // Create expanded bundle with patient resources only (no shared resources from manifest)
                var expandedBundle = new Bundle
                {
                    Type = Bundle.BundleType.Collection,
                    Timestamp = DateTimeOffset.UtcNow
                };

                expandedBundle.Entry.Add(new Bundle.EntryComponent
                {
                    FullUrl = $"http://www.cdc.gov/nhsn/fhirportal/dqm/ig/MeasureReport/{patientMR.Id}",
                    Resource = patientMR
                });

                // Add Organization to each patient bundle
                if (organization != null)
                {
                    expandedBundle.Entry.Add(new Bundle.EntryComponent
                    {
                        FullUrl = $"http://www.cdc.gov/nhsn/fhirportal/dqm/ig/Organization/{organization.Id}",
                        Resource = organization
                    });
                }

                foreach (var resource in patientResources.Where(r => r != patientMR))
                {
                    expandedBundle.Entry.Add(new Bundle.EntryComponent
                    {
                        FullUrl = $"http://www.cdc.gov/nhsn/fhirportal/dqm/ig/{resource.TypeName}/{resource.Id}",
                        Resource = resource
                    });
                }

                var bundleJson = JsonSerializer.Serialize(expandedBundle, jsonOptions);
                byte[] bundleBytes = Encoding.UTF8.GetBytes(bundleJson);

                string bundleName = $"{nhsnOrgId}_{measureAcronym}_{startDateStr}_patient-{patientId}.json";
                string blobName = GetBlobName(_externalSettings.BlobRoot, measureFolder, reportName, bundleName);
                _logger.LogDebug("Uploading expanded patient bundle: {BlobName}", blobName);

                BlockBlobClient blobClient = _externalContainerClient!.GetBlockBlobClient(blobName);
                BlockBlobOpenWriteOptions blobOptions = new()
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
                };

                await using var stream = await blobClient.OpenWriteAsync(true, blobOptions, cancellationToken);
                await stream.WriteAsync(bundleBytes, cancellationToken);
            }

            // Upload aggregate reports
            foreach (var aggregate in aggregateReports)
            {
                var aggregateJson = JsonSerializer.Serialize(aggregate, jsonOptions);
                byte[] aggregateBytes = Encoding.UTF8.GetBytes(aggregateJson);

                string measureName = aggregate.Measure.Split('/').Last().Split('|')[0];
                string aggregateName = $"aggregate-{measureName}.json";

                string blobName = GetBlobName(_externalSettings.BlobRoot, measureFolder, reportName, aggregateName);
                _logger.LogDebug("Uploading aggregate report: {BlobName}", blobName);

                BlockBlobClient blobClient = _externalContainerClient!.GetBlockBlobClient(blobName);
                BlockBlobOpenWriteOptions blobOptions = new()
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
                };

                await using var stream = await blobClient.OpenWriteAsync(true, blobOptions, cancellationToken);
                await stream.WriteAsync(aggregateBytes, cancellationToken);
            }

            if (!aggregateReports.Any() && manifestResources.OfType<MeasureReport>().Any(m => m.Type?.ToString()?.ToLowerInvariant().Contains("subject-list") == true))
            {
                var fallbackAgg = manifestResources.OfType<MeasureReport>().First(m => m.Type?.ToString()?.ToLowerInvariant().Contains("subject-list") == true);
                _logger.LogWarning("Fallback: Uploading aggregate report {Id} that was not classified as aggregate", fallbackAgg.Id);

                var aggregateJson = JsonSerializer.Serialize(fallbackAgg, jsonOptions);
                byte[] aggregateBytes = Encoding.UTF8.GetBytes(aggregateJson);
                string measureName = fallbackAgg.Measure.Split('/').Last().Split('|')[0];
                string aggregateName = $"aggregate-{measureName}.json";
                string blobName = GetBlobName(_externalSettings.BlobRoot, measureFolder, reportName, aggregateName);
                _logger.LogDebug("Fallback uploading aggregate: {BlobName}", blobName);

                BlockBlobClient blobClient = _externalContainerClient!.GetBlockBlobClient(blobName);
                BlockBlobOpenWriteOptions blobOptions = new() { HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" } };
                await using var stream = await blobClient.OpenWriteAsync(true, blobOptions, cancellationToken);
                await stream.WriteAsync(aggregateBytes, cancellationToken);
            }

            // shared-resources.json - now an EMPTY Bundle
            var sharedBundle = new Bundle
            {
                Type = Bundle.BundleType.Collection,
                Timestamp = DateTimeOffset.UtcNow
            };
            // No entries added - empty bundle

            var sharedJson = JsonSerializer.Serialize(sharedBundle, jsonOptions);
            byte[] sharedBytes = Encoding.UTF8.GetBytes(sharedJson);

            string sharedBlobName = GetBlobName(_externalSettings.BlobRoot, measureFolder, reportName, "shared-resources.json");
            _logger.LogDebug("Uploading empty shared resources bundle: {BlobName}", sharedBlobName);

            BlockBlobClient sharedClient = _externalContainerClient!.GetBlockBlobClient(sharedBlobName);
            BlockBlobOpenWriteOptions sharedOptions = new()
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
            };

            await using var sharedStream = await sharedClient.OpenWriteAsync(true, sharedOptions, cancellationToken);
            await sharedStream.WriteAsync(sharedBytes, cancellationToken);

            // census.json - use the List from manifest as-is
            if (censusList != null)
            {
                var censusJson = JsonSerializer.Serialize(censusList, jsonOptions);
                byte[] censusBytes = Encoding.UTF8.GetBytes(censusJson);

                string censusBlobName = GetBlobName(_externalSettings.BlobRoot, measureFolder, reportName, "census.json");
                _logger.LogDebug("Uploading census file: {BlobName}", censusBlobName);

                BlockBlobClient censusClient = _externalContainerClient!.GetBlockBlobClient(censusBlobName);
                await using var censusStream = await censusClient.OpenWriteAsync(true, sharedOptions, cancellationToken);
                await censusStream.WriteAsync(censusBytes, cancellationToken);
            }
            else
            {
                _logger.LogWarning("No census List found in manifest - skipping census.json upload");
            }

            // submitting-org.json - use Organization from manifest
            if (organization != null)
            {
                // Inject dummy address if missing or only contains data-absent-reason extensions
                bool needsDummyAddress = organization.Address == null ||
                                         !organization.Address.Any() ||
                                         organization.Address.All(a =>
                                             string.IsNullOrEmpty(a.City) &&
                                             string.IsNullOrEmpty(a.State) &&
                                             (a.Line == null || !a.Line.Any()));

                if (needsDummyAddress)
                {
                    organization.Address = new List<Address>
                    {
                        new Address
                        {
                            Line = new List<string> { "1 Center Drive" },
                            City = "Ann Arbor",
                            State = "MI",
                            PostalCode = "48109",
                            Country = "USA"
                        }
                    };
                    _logger.LogDebug("Injected dummy address for Organization {OrgId}", organization.Id ?? "no-id");
                }

                var orgJson = JsonSerializer.Serialize(organization, jsonOptions);
                byte[] orgBytes = Encoding.UTF8.GetBytes(orgJson);
                string orgBlobName = GetBlobName(_externalSettings.BlobRoot, measureFolder, reportName, "submitting-org.json");
                _logger.LogDebug("Uploading submitting organization: {BlobName}", orgBlobName);

                BlockBlobClient orgClient = _externalContainerClient!.GetBlockBlobClient(orgBlobName);
                await using var orgStream = await orgClient.OpenWriteAsync(true, sharedOptions, cancellationToken);
                await orgStream.WriteAsync(orgBytes, cancellationToken);
            }
            else
            {
                _logger.LogWarning("No Organization found in manifest - skipping submitting-org.json upload");
            }

            // submitting-device.json - use Device from manifest
            if (device != null)
            {
                var deviceJson = JsonSerializer.Serialize(device, jsonOptions);
                byte[] deviceBytes = Encoding.UTF8.GetBytes(deviceJson);
                string deviceBlobName = GetBlobName(_externalSettings.BlobRoot, measureFolder, reportName, "submitting-device.json");
                _logger.LogDebug("Uploading submitting device: {BlobName}", deviceBlobName);

                BlockBlobClient deviceClient = _externalContainerClient!.GetBlockBlobClient(deviceBlobName);
                await using var deviceStream = await deviceClient.OpenWriteAsync(true, sharedOptions, cancellationToken);
                await deviceStream.WriteAsync(deviceBytes, cancellationToken);
            }
            else
            {
                _logger.LogWarning("No Device found in manifest - skipping submitting-device.json upload");
            }

            // validation.json - use OperationOutcome from manifest
            if (operationOutcome != null)
            {
                var validationJson = JsonSerializer.Serialize(operationOutcome, jsonOptions);
                byte[] validationBytes = Encoding.UTF8.GetBytes(validationJson);
                string validationBlobName = GetBlobName(_externalSettings.BlobRoot, measureFolder, reportName, "validation.json");
                _logger.LogDebug("Uploading validation file: {BlobName}", validationBlobName);

                BlockBlobClient validationClient = _externalContainerClient!.GetBlockBlobClient(validationBlobName);
                await using var validationStream = await validationClient.OpenWriteAsync(true, sharedOptions, cancellationToken);
                await validationStream.WriteAsync(validationBytes, cancellationToken);
            }
            else
            {
                _logger.LogWarning("No OperationOutcome found in manifest - skipping validation.json upload");
            }

            _logger.LogInformation("Completed processing and uploading {PatientCount} expanded patient bundles", patientIds.Count);
        }

        private static string PatchEmptyDeviceVersionValue(string json)
        {
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("resourceType", out var rt) ||
                rt.GetString() != "Device")
            {
                return json;
            }

            if (!doc.RootElement.TryGetProperty("version", out var versions) ||
                versions.ValueKind != JsonValueKind.Array)
            {
                return json;
            }

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);

            writer.WriteStartObject();

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.NameEquals("version"))
                {
                    writer.WritePropertyName("version");
                    writer.WriteStartArray();

                    foreach (var v in prop.Value.EnumerateArray())
                    {
                        writer.WriteStartObject();

                        if (v.TryGetProperty("value", out var val) &&
                            val.ValueKind == JsonValueKind.String &&
                            string.IsNullOrWhiteSpace(val.GetString()))
                        {
                            // 🔑 PLACEHOLDER
                            writer.WriteString("value", "unknown");
                        }
                        else
                        {
                            foreach (var inner in v.EnumerateObject())
                                inner.WriteTo(writer);
                        }

                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
            writer.Flush();

            return Encoding.UTF8.GetString(stream.ToArray());
        }


        private bool IsAggregateMeasureReport(MeasureReport measureReport)
        {
            var typeCode = measureReport.Type?.ToString()?.ToLowerInvariant() ?? "null";
            var hasSubject = measureReport.Subject != null;
            var hasSubjectListRef = measureReport.Contained?.Any(c => c is Hl7.Fhir.Model.List) == true;

            _logger.LogDebug("Aggregate check for MeasureReport {Id}: type={Type}, hasSubject={HasSubject}, hasContainedList={HasList}",
                measureReport.Id ?? "no-id", typeCode, hasSubject, hasSubjectListRef);

            bool isSummaryType = typeCode == "summary" ||
                                 typeCode == "subject-list" ||
                                 typeCode == "subjectlist";

            return isSummaryType && !hasSubject;
        }

        private string GetMeasureFolderPath(List<string> reportTypes)
        {
            if (reportTypes == null || reportTypes.Count == 0)
                return string.Empty;

            var reportTypesStr = string.Join(",", reportTypes).ToLowerInvariant();

            // Map report types to folder names based on upload_file.py logic
            if (reportTypesStr.Contains("ach") && reportTypesStr.Contains("hypo"))
            {
                return "NHSNdQMAcuteCareHospitalInitialPopulation_NHSNGlycemicControlHypoglycemicInitialPopulation";
            }
            else if (reportTypesStr.Contains("rps"))
            {
                return "NHSNRespiratoryPathogensSurveillanceInitialPopulation";
            }

            // Fallback: use the first report type or empty string
            return reportTypes.FirstOrDefault() ?? string.Empty;
        }

        private HashSet<(string ResourceType, string ResourceId)> FindReferencesInResources(List<Resource> resources)
        {
            var references = new HashSet<(string, string)>();
            var jsonOptions = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);

            foreach (var resource in resources)
            {
                try
                {
                    var json = JsonSerializer.Serialize(resource, jsonOptions);
                    var doc = JsonDocument.Parse(json);

                    FindReferencesInJson(doc.RootElement, references);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to find references in resource {}", resource.TypeName);
                }
            }

            return references;
        }

        private void FindReferencesInJson(JsonElement element, HashSet<(string, string)> references)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    // Check if this object has a "reference" property
                    if (element.TryGetProperty("reference", out var refProp) &&
                        refProp.ValueKind == JsonValueKind.String)
                    {
                        var refValue = refProp.GetString();
                        if (!string.IsNullOrEmpty(refValue) &&
                            !refValue.Contains("://") &&
                            !refValue.StartsWith("urn:"))
                        {
                            var parts = refValue.Split('/');
                            if (parts.Length == 2)
                            {
                                references.Add((parts[0], parts[1]));
                            }
                        }
                    }

                    // Recursively check all properties
                    foreach (var prop in element.EnumerateObject())
                    {
                        FindReferencesInJson(prop.Value, references);
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        FindReferencesInJson(item, references);
                    }
                    break;
            }
        }

        private async Task<IDictionary<string, byte[]>> DownloadAsync(BlobContainerClient containerClient, string prefix, CancellationToken cancellationToken = default)
        {
            IDictionary<string, byte[]> files = new Dictionary<string, byte[]>();
            await foreach (BlobItem blob in containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken))
            {
                _logger.LogDebug("Downloading: {}", blob.Name);
                BlockBlobClient blobClient = containerClient.GetBlockBlobClient(blob.Name);
                using Stream input = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
                using MemoryStream output = new();
                await input.CopyToAsync(output, cancellationToken);
                _logger.LogDebug("Downloaded: {} byte(s)", output.Length);
                string fileName = blob.Name.Split('/').Last();
                files.Add(fileName, output.ToArray());
            }
            return files;
        }

        public Task<IDictionary<string, byte[]>> DownloadFromInternalAsync(string payloadRootUri, CancellationToken cancellationToken = default)
        {
            if (!HasInternalClient())
            {
                throw new InvalidOperationException("Not configured for internal blob storage.");
            }
            BlobUriBuilder uriBuilder = new(new Uri(payloadRootUri));
            string prefix = uriBuilder.BlobName;
            return DownloadAsync(_internalContainerClient, prefix, cancellationToken);
        }

        public Task<IDictionary<string, byte[]>> DownloadFromExternalAsync(string payloadRootUri, CancellationToken cancellationToken = default)
        {
            if (!HasExternalClient())
            {
                throw new InvalidOperationException("Not configured for external blob storage.");
            }
            BlobUriBuilder uriBuilder = new(new Uri(payloadRootUri));
            string prefix = ChangeBlobRoot(uriBuilder.BlobName);
            return DownloadAsync(_externalContainerClient, prefix, cancellationToken);
        }

        private string GetMeasureAcronym(List<string> reportTypes)
        {
            if (reportTypes == null || reportTypes.Count == 0)
                return "unknown";

            var reportTypesStr = string.Join(",", reportTypes).ToLowerInvariant();

            // Map report types to short acronyms
            if (reportTypesStr.Contains("nhsnacutecarehospitalinitialpopulation"))
            {
                return "ach1";
            }
            else if (reportTypesStr.Contains("respiratorypathogenssurveillance") ||
                     reportTypesStr.Contains("rps"))
            {
                return "rps";
            }
            else if (reportTypesStr.Contains("glycemiccontrol") ||
                     reportTypesStr.Contains("hypo"))
            {
                return "hypo";
            }

            // Fallback: use first report type as-is or first 10 chars
            var firstType = reportTypes.First().ToLowerInvariant();
            return firstType.Length > 10 ? firstType.Substring(0, 10) : firstType;
        }
    }
}