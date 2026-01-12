using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Submission.Application.Config;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Submission.Application.Services
{
    public class BlobStorageService
    {
        private readonly ILogger<BlobStorageService> _logger;
        private readonly InternalBlobStorageSettings _internalSettings;
        private readonly ExternalBlobStorageSettings _externalSettings;
        private readonly BlobContainerClient? _internalContainerClient;
        private readonly BlobContainerClient? _externalContainerClient;
        private readonly bool _useNdJson;

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

            _logger.LogWarning("_useNdJson is set to {}", _useNdJson);
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
                    // This is the manifest - process and upload all expanded patient bundles
                    await ProcessAndUploadExpandedBundlesAsync(key, value, cancellationToken);
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
                    ContentType = "application/x-ndjson"
                }
            };
            using Stream stream = await blobClient.OpenWriteAsync(true, blobOptions, cancellationToken);
            await stream.WriteAsync(content, cancellationToken);
        }

        //private async Task ProcessAndUploadExpandedBundlesAsync(
        //    SubmitPayloadKey key,
        //    SubmitPayloadValue value,
        //    CancellationToken cancellationToken)
        //{
        //    _logger.LogInformation("Processing expanded bundles for report");

        //    var jsonOptions = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector, new FhirJsonPocoDeserializerSettings { Validator = null });

        //    // Get the root URI from the manifest's PayloadUri
        //    var uriBuilder = new BlobUriBuilder(new Uri(value.PayloadUri));
        //    var pathParts = uriBuilder.BlobName.Split('/');
        //    var rootPath = string.Join('/', pathParts.Take(pathParts.Length - 1));
        //    var rootUri = $"{uriBuilder.Scheme}://{uriBuilder.Host}/{uriBuilder.BlobContainerName}/{rootPath}";

        //    // Download all files from internal storage
        //    _logger.LogDebug("Downloading all files from internal storage: {}", rootUri);
        //    var files = await DownloadFromInternalAsync(rootUri, cancellationToken);

        //    // Parse and categorize all resources
        //    var patientResources = new Dictionary<string, List<Resource>>();
        //    var sharedResources = new List<Resource>();
        //    Organization submittingOrg = null;
        //    Device submittingDevice = null;
        //    var aggregateReports = new List<MeasureReport>();
        //    string nhsnOrgId = null;

        //    foreach (var file in files)
        //    {
        //        _logger.LogDebug("Processing file: {}", file.Key);
        //        var ndjsonContent = Encoding.UTF8.GetString(file.Value);
        //        var lines = ndjsonContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        //        foreach (var line in lines)
        //        {
        //            if (string.IsNullOrWhiteSpace(line)) continue;

        //            var resource = JsonSerializer.Deserialize<Resource>(line, jsonOptions);

        //            // Categorize the resource
        //            if (file.Key.StartsWith("patient-"))
        //            {
        //                // Extract patient ID from filename
        //                var patientId = file.Key.Replace("patient-", "").Replace(".ndjson", "");
        //                if (!patientResources.ContainsKey(patientId))
        //                {
        //                    patientResources[patientId] = new List<Resource>();
        //                }
        //                patientResources[patientId].Add(resource);
        //            }
        //            else if (resource is Organization org)
        //            {
        //                submittingOrg = org;

        //                // Extract NHSN org ID
        //                var nhsnIdentifier = org.Identifier?.FirstOrDefault(
        //                    i => i.System == "https://www.cdc.gov/nhsn/OrgID");
        //                if (nhsnIdentifier != null)
        //                {
        //                    nhsnOrgId = nhsnIdentifier.Value;
        //                }
        //            }
        //            else if (resource is Device device)
        //            {
        //                submittingDevice = device;
        //            }
        //            else if (resource is MeasureReport mr && IsAggregateMeasureReport(mr))
        //            {
        //                aggregateReports.Add(mr);
        //            }
        //            else
        //            {
        //                // Everything else goes into shared resources
        //                sharedResources.Add(resource);
        //            }
        //        }
        //    }

        //    _logger.LogInformation("Found {} patient bundles, {} shared resources, {} aggregate reports",
        //        patientResources.Count, sharedResources.Count, aggregateReports.Count);

        //    // Build a lookup for shared resources
        //    var sharedResourceLookup = new Dictionary<(string ResourceType, string ResourceId), Resource>();
        //    foreach (var resource in sharedResources)
        //    {
        //        sharedResourceLookup[(resource.TypeName, resource.Id)] = resource;
        //    }

        //    // Process each patient and create expanded bundles
        //    string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        //    string measureFolder = GetMeasureFolderPath(value.ReportTypes);
        //    string reportName = ReportHelpers.GetReportName(key.ReportScheduleId, key.FacilityId, value.ReportTypes, value.StartDate);

        //    foreach (var patientEntry in patientResources)
        //    {
        //        string patientId = patientEntry.Key;
        //        var resources = patientEntry.Value;

        //        _logger.LogDebug("Creating expanded bundle for patient: {}", patientId);

        //        // Create the expanded bundle
        //        var expandedBundle = new Bundle
        //        {
        //            Type = Bundle.BundleType.Collection,
        //            Entry = new List<Bundle.EntryComponent>()
        //        };

        //        // Add original patient resources
        //        foreach (var resource in resources)
        //        {
        //            expandedBundle.Entry.Add(new Bundle.EntryComponent
        //            {
        //                FullUrl = resource.Id,
        //                Resource = resource
        //            });
        //        }

        //        // Find all references in the patient's resources
        //        var references = FindReferencesInResources(resources);

        //        // Add referenced shared resources
        //        foreach (var reference in references)
        //        {
        //            if (sharedResourceLookup.TryGetValue(reference, out var sharedResource))
        //            {
        //                expandedBundle.Entry.Add(new Bundle.EntryComponent
        //                {
        //                    FullUrl = sharedResource.Id,
        //                    Resource = sharedResource
        //                });
        //            }
        //        }

        //        // Add submitting org and device
        //        if (submittingOrg != null)
        //        {
        //            expandedBundle.Entry.Add(new Bundle.EntryComponent
        //            {
        //                FullUrl = submittingOrg.Id,
        //                Resource = submittingOrg
        //            });
        //        }

        //        if (submittingDevice != null)
        //        {
        //            expandedBundle.Entry.Add(new Bundle.EntryComponent
        //            {
        //                FullUrl = submittingDevice.Id,
        //                Resource = submittingDevice
        //            });
        //        }

        //        // Add all aggregate reports
        //        foreach (var aggregate in aggregateReports)
        //        {
        //            expandedBundle.Entry.Add(new Bundle.EntryComponent
        //            {
        //                FullUrl = aggregate.Id,
        //                Resource = aggregate
        //            });
        //        }

        //        // Serialize the expanded bundle
        //        var bundleJson = JsonSerializer.Serialize(expandedBundle, jsonOptions);
        //        var bundleBytes = Encoding.UTF8.GetBytes(bundleJson);

        //        // Create the filename with NHSN org ID and timestamp
        //        string bundleName;
        //        if (!string.IsNullOrEmpty(nhsnOrgId))
        //        {
        //            bundleName = $"patient-{nhsnOrgId}-{patientId}-{timestamp}.json";
        //        }
        //        else
        //        {
        //            bundleName = $"patient-{patientId}-{timestamp}.json";
        //        }

        //        // Upload to external storage
        //        string blobName = GetBlobName(_externalSettings.BlobRoot, measureFolder, reportName, bundleName);
        //        _logger.LogDebug("Uploading expanded bundle: {}", blobName);

        //        BlockBlobClient blobClient = _externalContainerClient.GetBlockBlobClient(blobName);
        //        BlockBlobOpenWriteOptions blobOptions = new()
        //        {
        //            HttpHeaders = new()
        //            {
        //                ContentType = "application/json"
        //            }
        //        };

        //        using Stream stream = await blobClient.OpenWriteAsync(true, blobOptions, cancellationToken);
        //        await stream.WriteAsync(bundleBytes, cancellationToken);
        //    }

        //    _logger.LogInformation("Completed uploading {} expanded patient bundles", patientResources.Count);
        //}

        //private async Task ProcessAndUploadExpandedBundlesAsync(
        //    SubmitPayloadKey key,
        //    SubmitPayloadValue value,
        //    CancellationToken cancellationToken)
        //{
        //    _logger.LogInformation("Processing expanded bundles for report");

        //    var jsonOptions = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector, new FhirJsonPocoDeserializerSettings { Validator = null });

        //    // Get the root URI from the manifest's PayloadUri
        //    var uriBuilder = new BlobUriBuilder(new Uri(value.PayloadUri));
        //    var pathParts = uriBuilder.BlobName.Split('/');
        //    var rootPath = string.Join('/', pathParts.Take(pathParts.Length - 1));
        //    var rootUri = $"{uriBuilder.Scheme}://{uriBuilder.Host}/{uriBuilder.BlobContainerName}/{rootPath}";

        //    // Download all files from internal storage
        //    _logger.LogDebug("Downloading all files from internal storage: {}", rootUri);
        //    var files = await DownloadFromInternalAsync(rootUri, cancellationToken);

        //    // Parse and categorize all resources
        //    var patientResources = new Dictionary<string, List<Resource>>();
        //    Organization submittingOrg = null;
        //    Device submittingDevice = null;
        //    var aggregateReports = new List<MeasureReport>();
        //    List censusList = null;
        //    byte[] queryPlanContent = null;
        //    string nhsnOrgId = null;

        //    foreach (var file in files)
        //    {
        //        _logger.LogDebug("Processing file: {}", file.Key);

        //        // Handle query-plan.yml separately (it's not JSON)
        //        if (file.Key == "query-plan.yml" || file.Key.EndsWith(".yml") || file.Key.EndsWith(".yaml"))
        //        {
        //            queryPlanContent = file.Value;
        //            continue;
        //        }

        //        var ndjsonContent = Encoding.UTF8.GetString(file.Value);
        //        var lines = ndjsonContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        //        foreach (var line in lines)
        //        {
        //            if (string.IsNullOrWhiteSpace(line)) continue;

        //            var resource = JsonSerializer.Deserialize<Resource>(line, jsonOptions);

        //            // Categorize the resource
        //            if (file.Key.StartsWith("patient-"))
        //            {
        //                // Extract patient ID from filename
        //                var patientId = file.Key.Replace("patient-", "").Replace(".ndjson", "");
        //                if (!patientResources.ContainsKey(patientId))
        //                {
        //                    patientResources[patientId] = new List<Resource>();
        //                }
        //                patientResources[patientId].Add(resource);
        //            }
        //            else if (resource is Organization org)
        //            {
        //                submittingOrg = org;

        //                // Extract NHSN org ID
        //                var nhsnIdentifier = org.Identifier?.FirstOrDefault(
        //                    i => i.System == "https://www.cdc.gov/nhsn/OrgID");
        //                if (nhsnIdentifier != null)
        //                {
        //                    nhsnOrgId = nhsnIdentifier.Value;
        //                }
        //            }
        //            else if (resource is Device device)
        //            {
        //                submittingDevice = device;
        //            }
        //            else if (resource is MeasureReport mr && IsAggregateMeasureReport(mr))
        //            {
        //                aggregateReports.Add(mr);
        //            }
        //            else if (resource is List list)
        //            {
        //                censusList = list;
        //            }
        //        }
        //    }

        //    _logger.LogInformation("Found {} patient bundles, {} aggregate reports",
        //        patientResources.Count, aggregateReports.Count);

        //    string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        //    string measureFolder = GetMeasureFolderPath(value.ReportTypes);
        //    string reportName = ReportHelpers.GetReportName(key.ReportScheduleId, key.FacilityId, value.ReportTypes, value.StartDate);

        //    // Upload supplemental files first

        //    // 1. Upload empty shared-resources.json
        //    var emptySharedResources = new Bundle
        //    {
        //        Type = Bundle.BundleType.Collection,
        //        Timestamp = DateTimeOffset.UtcNow
        //    };
        //    await UploadSupplementalFile(
        //        "shared-resources.json",
        //        emptySharedResources,
        //        measureFolder,
        //        reportName,
        //        timestamp,
        //        jsonOptions,
        //        cancellationToken);

        //    // 2. Upload submitting-device.json
        //    if (submittingDevice != null)
        //    {
        //        await UploadSupplementalFile(
        //            "submitting-device.json",
        //            submittingDevice,
        //            measureFolder,
        //            reportName,
        //            timestamp,
        //            jsonOptions,
        //            cancellationToken);
        //    }

        //    // 3. Upload submitting-org.json
        //    if (submittingOrg != null)
        //    {
        //        await UploadSupplementalFile(
        //            "submitting-org.json",
        //            submittingOrg,
        //            measureFolder,
        //            reportName,
        //            timestamp,
        //            jsonOptions,
        //            cancellationToken);
        //    }

        //    // 4. Upload aggregate measure reports
        //    foreach (var aggregate in aggregateReports)
        //    {
        //        // Extract measure name from the measure URL
        //        var measureName = ExtractMeasureName(aggregate.Measure);
        //        var fileName = $"aggregate-{measureName}.json";

        //        await UploadSupplementalFile(
        //            fileName,
        //            aggregate,
        //            measureFolder,
        //            reportName,
        //            timestamp,
        //            jsonOptions,
        //            cancellationToken);
        //    }

        //    // 5. Upload census.json
        //    if (censusList != null)
        //    {
        //        await UploadSupplementalFile(
        //            "census.json",
        //            censusList,
        //            measureFolder,
        //            reportName,
        //            timestamp,
        //            jsonOptions,
        //            cancellationToken);
        //    }

        //    // 6. Upload query-plan.yml
        //    if (queryPlanContent != null)
        //    {
        //        string blobName = GetBlobName(_externalSettings.BlobRoot, measureFolder, reportName, "query-plan");
        //        _logger.LogDebug("Uploading query-plan");

        //        BlockBlobClient blobClient = _externalContainerClient.GetBlockBlobClient(blobName);
        //        BlockBlobOpenWriteOptions blobOptions = new()
        //        {
        //            HttpHeaders = new()
        //            {
        //                ContentType = "application/x-yaml"
        //            }
        //        };

        //        using Stream stream = await blobClient.OpenWriteAsync(true, blobOptions, cancellationToken);
        //        await stream.WriteAsync(queryPlanContent, cancellationToken);
        //    }

        //    // Now process each patient and create expanded bundles
        //    foreach (var patientEntry in patientResources)
        //    {
        //        string patientId = patientEntry.Key;
        //        var resources = patientEntry.Value;

        //        _logger.LogDebug("Creating expanded bundle for patient: {}", patientId);

        //        // Create the expanded bundle
        //        var expandedBundle = new Bundle
        //        {
        //            Type = Bundle.BundleType.Collection,
        //            Entry = new List<Bundle.EntryComponent>()
        //        };

        //        // Add original patient resources
        //        foreach (var resource in resources)
        //        {
        //            expandedBundle.Entry.Add(new Bundle.EntryComponent
        //            {
        //                FullUrl = resource.Id,
        //                Resource = resource
        //            });
        //        }

        //        // Find all references in the patient's resources - but we're not adding shared resources anymore
        //        // var references = FindReferencesInResources(resources);

        //        // Add submitting org and device
        //        if (submittingOrg != null)
        //        {
        //            expandedBundle.Entry.Add(new Bundle.EntryComponent
        //            {
        //                FullUrl = submittingOrg.Id,
        //                Resource = submittingOrg
        //            });
        //        }

        //        if (submittingDevice != null)
        //        {
        //            expandedBundle.Entry.Add(new Bundle.EntryComponent
        //            {
        //                FullUrl = submittingDevice.Id,
        //                Resource = submittingDevice
        //            });
        //        }

        //        // Add all aggregate reports
        //        foreach (var aggregate in aggregateReports)
        //        {
        //            expandedBundle.Entry.Add(new Bundle.EntryComponent
        //            {
        //                FullUrl = aggregate.Id,
        //                Resource = aggregate
        //            });
        //        }

        //        // Serialize the expanded bundle
        //        var bundleJson = JsonSerializer.Serialize(expandedBundle, jsonOptions);
        //        var bundleBytes = Encoding.UTF8.GetBytes(bundleJson);

        //        // Create the filename with NHSN org ID, report name, date, and timestamp
        //        string orgId = "00000";
        //        string startDate = value.StartDate?.ToString("yyyyMMdd") ?? DateTime.UtcNow.ToString("yyyyMMdd");
        //        string reportTypesStr = string.Join('+', value.ReportTypes.Order());

        //        string bundleName = $"{orgId}_{reportTypesStr}_{startDate}_{timestamp}_patient-{patientId}.json";

        //        // Upload to external storage
        //        string blobName = GetBlobName(_externalSettings.BlobRoot, measureFolder, reportName, bundleName);
        //        _logger.LogDebug("Uploading expanded bundle: {}", blobName);

        //        BlockBlobClient blobClient = _externalContainerClient.GetBlockBlobClient(blobName);
        //        BlockBlobOpenWriteOptions blobOptions = new()
        //        {
        //            HttpHeaders = new()
        //            {
        //                ContentType = "application/json"
        //            }
        //        };

        //        using Stream stream = await blobClient.OpenWriteAsync(true, blobOptions, cancellationToken);
        //        await stream.WriteAsync(bundleBytes, cancellationToken);
        //    }

        //    _logger.LogInformation("Completed uploading {} expanded patient bundles and supplemental files", patientResources.Count);
        //}

        private async Task ProcessAndUploadExpandedBundlesAsync(
            SubmitPayloadKey key,
            SubmitPayloadValue value,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(value.PayloadRootUri))
            {
                _logger.LogWarning("No PayloadRootUri provided for expanded bundle processing");
                return;
            }

            _logger.LogInformation("Starting expanded bundle processing for report {ReportScheduleId}", key.ReportScheduleId);

            // 1. Download the manifest NDJSON from internal storage
            var manifestFiles = await DownloadFromInternalAsync(value.PayloadRootUri, cancellationToken);
            if (!manifestFiles.Any())
            {
                _logger.LogWarning("No files found under manifest prefix: {PayloadRootUri}", value.PayloadRootUri);
                return;
            }

            // Assume manifest is the file named manifest.ndjson or similar
            var manifestFile = manifestFiles.FirstOrDefault(f => f.Key.Contains("manifest") || f.Key.EndsWith(".ndjson"));
            if (manifestFile.Value == null || manifestFile.Value.Length == 0)
            {
                _logger.LogWarning("Manifest content not found or empty");
                return;
            }

            string manifestContent = Encoding.UTF8.GetString(manifestFile.Value);
            var jsonOptions = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
            var manifestResources = new List<Resource>();

            foreach (var line in manifestContent.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var resource = JsonSerializer.Deserialize<Resource>(line, jsonOptions);
                    if (resource != null) manifestResources.Add(resource);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse manifest line");
                }
            }

            var patientMeasureReports = manifestResources
                .OfType<MeasureReport>()
                .Where(mr => !IsAggregateMeasureReport(mr))
                .ToList();

            var aggregateMeasureReports = manifestResources
                .OfType<MeasureReport>()
                .Where(IsAggregateMeasureReport)
                .ToList();

            var sharedResources = manifestResources
                .Where(r => r is not MeasureReport)
                .ToList();

            string reportName = ReportHelpers.GetReportName(
                key.ReportScheduleId,
                key.FacilityId,
                value.ReportTypes,
                value.StartDate);

            string measureFolder = GetMeasureFolderPath(value.ReportTypes);

            string? nhsnOrgId = sharedResources
                .OfType<Organization>()
                .FirstOrDefault()
                ?.Identifier
                ?.FirstOrDefault(i => i.System?.Contains("nhsn/OrgID") == true)
                ?.Value;

            nhsnOrgId ??= key.FacilityId ?? "unknown";

            string startDateYmd = DateTime.Parse(value.StartDate).ToString("yyyyMMdd");
            string measureAcronym = value.ReportTypes.FirstOrDefault() ?? "unknown";

            // 2. Process each patient
            foreach (var patientMR in patientMeasureReports)
            {
                if (patientMR.Subject?.Reference == null) continue;

                string patientId = patientMR.Subject.Reference.Split('/').Last();

                // Download the patient's NDJSON from internal storage
                string patientPrefix = $"patient-{patientId}";
                var patientFiles = await DownloadFromInternalAsync(
                    $"{value.PayloadRootUri.TrimEnd('/')}/{patientPrefix}",
                    cancellationToken);

                var patientNdjsonFile = patientFiles.FirstOrDefault(f => f.Key.EndsWith(".ndjson"));
                if (patientNdjsonFile.Value == null || patientNdjsonFile.Value.Length == 0)
                {
                    _logger.LogWarning("No patient NDJSON found for {PatientId}", patientId);
                    continue;
                }

                string patientNdjson = Encoding.UTF8.GetString(patientNdjsonFile.Value);
                var patientResources = new List<Resource>();

                foreach (var line in patientNdjson.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    try
                    {
                        var res = JsonSerializer.Deserialize<Resource>(line, jsonOptions);
                        if (res != null) patientResources.Add(res);
                    }
                    catch { /* skip invalid lines */ }
                }

                // Find referenced shared resources
                var referencedIds = FindReferencesInResources(patientResources);
                var neededShared = sharedResources
                    .Where(r => referencedIds.Contains((r.TypeName, r.Id)))
                    .ToList();

                // Build expanded patient bundle
                var expandedBundle = new Bundle
                {
                    Type = Bundle.BundleType.Collection,
                    Timestamp = DateTimeOffset.UtcNow
                };

                // Add patient measure report
                expandedBundle.Entry.Add(new Bundle.EntryComponent
                {
                    FullUrl = patientMR.FullUrl() ?? $"urn:uuid:{patientMR.Id}",
                    Resource = patientMR
                });

                // Add all patient-specific resources
                foreach (var res in patientResources)
                {
                    expandedBundle.Entry.Add(new Bundle.EntryComponent
                    {
                        FullUrl = res.FullUrl() ?? $"urn:uuid:{res.Id}",
                        Resource = res
                    });
                }

                // Add referenced shared resources
                foreach (var res in neededShared)
                {
                    expandedBundle.Entry.Add(new Bundle.EntryComponent
                    {
                        FullUrl = res.FullUrl() ?? $"urn:uuid:{res.Id}",
                        Resource = res
                    });
                }

                // Serialize and upload
                string bundleJson = JsonSerializer.Serialize(expandedBundle, jsonOptions);
                byte[] bundleBytes = Encoding.UTF8.GetBytes(bundleJson);

                string filename = $"{nhsnOrgId}_{measureAcronym}_{startDateYmd}_patient-{patientId}.json";
                string blobPath = GetBlobName(_externalSettings.BlobRoot, measureFolder, reportName, filename);

                await UploadJsonBlobAsync(blobPath, bundleBytes, cancellationToken);
                _logger.LogInformation("Uploaded patient bundle: {BlobPath}", blobPath);
            }

            // 3. Upload aggregate measure report(s)
            foreach (var agg in aggregateMeasureReports)
            {
                string aggJson = JsonSerializer.Serialize(agg, jsonOptions);
                byte[] aggBytes = Encoding.UTF8.GetBytes(aggJson);

                string aggFilename = $"aggregate-{agg.Id}.json";  // or derive from measure URL if preferred
                string aggBlobPath = GetBlobName(_externalSettings.BlobRoot, measureFolder, reportName, aggFilename);

                await UploadJsonBlobAsync(aggBlobPath, aggBytes, cancellationToken);
                _logger.LogInformation("Uploaded aggregate: {BlobPath}", aggBlobPath);
            }

            // 4. Upload shared-resources.json (all shared resources in one bundle)
            if (sharedResources.Any())
            {
                var sharedBundle = new Bundle
                {
                    Type = Bundle.BundleType.Collection,
                    Timestamp = DateTimeOffset.UtcNow
                };

                foreach (var res in sharedResources)
                {
                    sharedBundle.Entry.Add(new Bundle.EntryComponent
                    {
                        FullUrl = res.FullUrl() ?? $"urn:uuid:{res.Id}",
                        Resource = res
                    });
                }

                string sharedJson = JsonSerializer.Serialize(sharedBundle, jsonOptions);
                byte[] sharedBytes = Encoding.UTF8.GetBytes(sharedJson);

                string sharedPath = GetBlobName(_externalSettings.BlobRoot, measureFolder, reportName, "shared-resources.json");
                await UploadJsonBlobAsync(sharedPath, sharedBytes, cancellationToken);
                _logger.LogInformation("Uploaded shared-resources: {BlobPath}", sharedPath);
            }

            // 5. Upload census.json
            var census = BuildCensusList(patientMeasureReports, value);
            if (census != null)
            {
                string censusJson = JsonSerializer.Serialize(census, jsonOptions);
                byte[] censusBytes = Encoding.UTF8.GetBytes(censusJson);

                string censusPath = GetBlobName(_externalSettings.BlobRoot, measureFolder, reportName, "census.json");
                await UploadJsonBlobAsync(censusPath, censusBytes, cancellationToken);
                _logger.LogInformation("Uploaded census: {BlobPath}", censusPath);
            }

            // 6. Upload submitting-org.json and submitting-device.json if present
            var org = sharedResources.OfType<Organization>().FirstOrDefault(o => o.Meta?.Profile?.Any(p => p.Contains("submitting-organization")) == true);
            if (org != null)
            {
                string orgJson = JsonSerializer.Serialize(org, jsonOptions);
                byte[] orgBytes = Encoding.UTF8.GetBytes(orgJson);
                string orgPath = GetBlobName(_externalSettings.BlobRoot, measureFolder, reportName, "submitting-org.json");
                await UploadJsonBlobAsync(orgPath, orgBytes, cancellationToken);
            }

            var device = sharedResources.OfType<Device>().FirstOrDefault(d => d.Meta?.Profile?.Any(p => p.Contains("submitting-device")) == true);
            if (device != null)
            {
                string deviceJson = JsonSerializer.Serialize(device, jsonOptions);
                byte[] deviceBytes = Encoding.UTF8.GetBytes(deviceJson);
                string devicePath = GetBlobName(_externalSettings.BlobRoot, measureFolder, reportName, "submitting-device.json");
                await UploadJsonBlobAsync(devicePath, deviceBytes, cancellationToken);
            }

            _logger.LogInformation("Completed expanded bundle processing for {PatientCount} patients", patientMeasureReports.Count);
        }

        // Helper to upload JSON content
        private async Task UploadJsonBlobAsync(string blobName, byte[] content, CancellationToken ct)
        {
            var blobClient = _externalContainerClient!.GetBlockBlobClient(blobName);
            var options = new BlockBlobOpenWriteOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
            };

            await using var stream = await blobClient.OpenWriteAsync(true, options, ct);
            await stream.WriteAsync(content, ct);
        }

        // Helper to build census List resource (matching your example)
        private Hl7.Fhir.Model.List? BuildCensusList(List<MeasureReport> patientMRs, SubmitPayloadValue value)
        {
            if (!patientMRs.Any()) return null;

            var census = new Hl7.Fhir.Model.List
            {
                Id = Guid.NewGuid().ToString("N"),
                Meta = new Meta { Profile = new[] { "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/poi-list" } },
                Status = Hl7.Fhir.Model.List.ListStatus.Current,
                Mode = Hl7.Fhir.Model.List.ListMode.Snapshot,
                Identifier = new List<Identifier>
        {
            new Identifier { System = "https://nhsnlink.org", Value = "NHSNAcuteCareHospitalMonthlyInitialPopulation" }
        },
                Extension = new List<Extension>
        {
            new Extension
            {
                Url = "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/link-patient-list-applicable-period-extension",
                Value = new Period { Start = value.StartDate, End = value.EndDate ?? value.StartDate }
            }
        }
            };

            foreach (var mr in patientMRs)
            {
                if (mr.Subject?.Reference != null)
                {
                    string patientId = mr.Subject.Reference.Split('/').Last();
                    census.Entry.Add(new Hl7.Fhir.Model.List.EntryComponent
                    {
                        Item = new ResourceReference($"Patient/{patientId}")
                    });
                }
            }

            return census;
        }

        private async Task UploadSupplementalFile(
            string fileName,
            Resource resource,
            string measureFolder,
            string reportName,
            string timestamp,
            JsonSerializerOptions jsonOptions,
            CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(resource, jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            string blobName = GetBlobName(_externalSettings.BlobRoot, measureFolder, reportName, fileName);
            _logger.LogDebug("Uploading supplemental file: {}", blobName);

            BlockBlobClient blobClient = _externalContainerClient.GetBlockBlobClient(blobName);
            BlockBlobOpenWriteOptions blobOptions = new()
            {
                HttpHeaders = new()
                {
                    ContentType = "application/json"
                }
            };

            using Stream stream = await blobClient.OpenWriteAsync(true, blobOptions, cancellationToken);
            await stream.WriteAsync(bytes, cancellationToken);
        }

        private string ExtractMeasureName(string measureUrl)
        {
            // Extract measure name from URL like:
            // "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/Measure/NHSNAcuteCareHospitalMonthlyInitialPopulation|1.0.0-dev"
            if (string.IsNullOrEmpty(measureUrl))
                return "Unknown";

            // Remove version if present
            var urlWithoutVersion = measureUrl.Split('|')[0];

            // Get the last segment
            var segments = urlWithoutVersion.Split('/');
            return segments.LastOrDefault() ?? "Unknown";
        }

        private bool IsAggregateMeasureReport(MeasureReport measureReport)
        {
            // Aggregate measure reports have type = "subject-list" and contain a reporter reference
            // They also don't have a subject (individual patient)
            return measureReport.Type == MeasureReport.MeasureReportType.SubjectList &&
                   measureReport.Subject == null;
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

        //private HashSet<(string ResourceType, string ResourceId)> FindReferencesInResources(List<Resource> resources)
        //{
        //    var references = new HashSet<(string, string)>();

        //    foreach (var resource in resources)
        //    {
        //        FindReferencesInResource(resource, references);
        //    }

        //    return references;
        //}

        //private void FindReferencesInResource(object obj, HashSet<(string, string)> references)
        //{
        //    if (obj == null) return;

        //    var properties = obj.GetType().GetProperties();

        //    foreach (var prop in properties)
        //    {
        //        var value = prop.GetValue(obj);
        //        if (value == null) continue;

        //        // Check if it's a ResourceReference
        //        if (value is ResourceReference reference && !string.IsNullOrEmpty(reference.Reference))
        //        {
        //            var parts = reference.Reference.Split('/');
        //            if (parts.Length == 2)
        //            {
        //                references.Add((parts[0], parts[1]));
        //            }
        //        }
        //        // Recursively check collections
        //        else if (value is System.Collections.IEnumerable enumerable && !(value is string))
        //        {
        //            foreach (var item in enumerable)
        //            {
        //                FindReferencesInResource(item, references);
        //            }
        //        }
        //        // Recursively check complex objects
        //        else if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
        //        {
        //            FindReferencesInResource(value, references);
        //        }
        //    }
        //}

        //private HashSet<(string ResourceType, string ResourceId)> FindReferencesInResources(List<Resource> resources)
        //{
        //    var references = new HashSet<(string, string)>();
        //    var jsonOptions = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);

        //    foreach (var resource in resources)
        //    {
        //        try
        //        {
        //            // Serialize to JSON and parse to find reference patterns
        //            var json = JsonSerializer.Serialize(resource, jsonOptions);
        //            var doc = JsonDocument.Parse(json);

        //            FindReferencesInJson(doc.RootElement, references);
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogWarning(ex, "Failed to find references in resource {}", resource.TypeName);
        //        }
        //    }

        //    return references;
        //}

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

        //========================================================================

        private async Task<IDictionary<string, byte[]>> DownloadAsync(BlobContainerClient containerClient, string prefix, CancellationToken cancellationToken = default)
        {
            IDictionary<string, byte[]> files = new Dictionary<string, byte[]>();
            await foreach (BlobItem blob in containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
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
    }
}
