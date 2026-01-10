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

        private async Task ProcessAndUploadExpandedBundlesAsync(
            SubmitPayloadKey key,
            SubmitPayloadValue value,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing expanded bundles for report");

            var jsonOptions = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector, new FhirJsonPocoDeserializerSettings { Validator = null });

            // Get the root URI from the manifest's PayloadUri
            var uriBuilder = new BlobUriBuilder(new Uri(value.PayloadUri));
            var pathParts = uriBuilder.BlobName.Split('/');
            var rootPath = string.Join('/', pathParts.Take(pathParts.Length - 1));
            var rootUri = $"{uriBuilder.Scheme}://{uriBuilder.Host}/{uriBuilder.BlobContainerName}/{rootPath}";

            // Download all files from internal storage
            _logger.LogDebug("Downloading all files from internal storage: {}", rootUri);
            var files = await DownloadFromInternalAsync(rootUri, cancellationToken);

            // Parse and categorize all resources
            var patientResources = new Dictionary<string, List<Resource>>();
            var sharedResources = new List<Resource>();
            Organization submittingOrg = null;
            Device submittingDevice = null;
            var aggregateReports = new List<MeasureReport>();
            string nhsnOrgId = null;

            foreach (var file in files)
            {
                _logger.LogDebug("Processing file: {}", file.Key);
                var ndjsonContent = Encoding.UTF8.GetString(file.Value);
                var lines = ndjsonContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var resource = JsonSerializer.Deserialize<Resource>(line, jsonOptions);

                    // Categorize the resource
                    if (file.Key.StartsWith("patient-"))
                    {
                        // Extract patient ID from filename
                        var patientId = file.Key.Replace("patient-", "").Replace(".ndjson", "");
                        if (!patientResources.ContainsKey(patientId))
                        {
                            patientResources[patientId] = new List<Resource>();
                        }
                        patientResources[patientId].Add(resource);
                    }
                    else if (resource is Organization org)
                    {
                        submittingOrg = org;

                        // Extract NHSN org ID
                        var nhsnIdentifier = org.Identifier?.FirstOrDefault(
                            i => i.System == "https://www.cdc.gov/nhsn/OrgID");
                        if (nhsnIdentifier != null)
                        {
                            nhsnOrgId = nhsnIdentifier.Value;
                        }
                    }
                    else if (resource is Device device)
                    {
                        submittingDevice = device;
                    }
                    else if (resource is MeasureReport mr && IsAggregateMeasureReport(mr))
                    {
                        aggregateReports.Add(mr);
                    }
                    else
                    {
                        // Everything else goes into shared resources
                        sharedResources.Add(resource);
                    }
                }
            }

            _logger.LogInformation("Found {} patient bundles, {} shared resources, {} aggregate reports",
                patientResources.Count, sharedResources.Count, aggregateReports.Count);

            // Build a lookup for shared resources
            var sharedResourceLookup = new Dictionary<(string ResourceType, string ResourceId), Resource>();
            foreach (var resource in sharedResources)
            {
                sharedResourceLookup[(resource.TypeName, resource.Id)] = resource;
            }

            // Process each patient and create expanded bundles
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            string measureFolder = GetMeasureFolderPath(value.ReportTypes);
            string reportName = ReportHelpers.GetReportName(key.ReportScheduleId, key.FacilityId, value.ReportTypes, value.StartDate);

            foreach (var patientEntry in patientResources)
            {
                string patientId = patientEntry.Key;
                var resources = patientEntry.Value;

                _logger.LogDebug("Creating expanded bundle for patient: {}", patientId);

                // Create the expanded bundle
                var expandedBundle = new Bundle
                {
                    Type = Bundle.BundleType.Collection,
                    Entry = new List<Bundle.EntryComponent>()
                };

                // Add original patient resources
                foreach (var resource in resources)
                {
                    expandedBundle.Entry.Add(new Bundle.EntryComponent
                    {
                        FullUrl = resource.Id,
                        Resource = resource
                    });
                }

                // Find all references in the patient's resources
                var references = FindReferencesInResources(resources);

                // Add referenced shared resources
                foreach (var reference in references)
                {
                    if (sharedResourceLookup.TryGetValue(reference, out var sharedResource))
                    {
                        expandedBundle.Entry.Add(new Bundle.EntryComponent
                        {
                            FullUrl = sharedResource.Id,
                            Resource = sharedResource
                        });
                    }
                }

                // Add submitting org and device
                if (submittingOrg != null)
                {
                    expandedBundle.Entry.Add(new Bundle.EntryComponent
                    {
                        FullUrl = submittingOrg.Id,
                        Resource = submittingOrg
                    });
                }

                if (submittingDevice != null)
                {
                    expandedBundle.Entry.Add(new Bundle.EntryComponent
                    {
                        FullUrl = submittingDevice.Id,
                        Resource = submittingDevice
                    });
                }

                // Add all aggregate reports
                foreach (var aggregate in aggregateReports)
                {
                    expandedBundle.Entry.Add(new Bundle.EntryComponent
                    {
                        FullUrl = aggregate.Id,
                        Resource = aggregate
                    });
                }

                // Serialize the expanded bundle
                var bundleJson = JsonSerializer.Serialize(expandedBundle, jsonOptions);
                var bundleBytes = Encoding.UTF8.GetBytes(bundleJson);

                // Create the filename with NHSN org ID and timestamp
                string bundleName;
                if (!string.IsNullOrEmpty(nhsnOrgId))
                {
                    bundleName = $"patient-{nhsnOrgId}-{patientId}-{timestamp}.json";
                }
                else
                {
                    bundleName = $"patient-{patientId}-{timestamp}.json";
                }

                // Upload to external storage
                string blobName = GetBlobName(_externalSettings.BlobRoot, measureFolder, reportName, bundleName);
                _logger.LogDebug("Uploading expanded bundle: {}", blobName);

                BlockBlobClient blobClient = _externalContainerClient.GetBlockBlobClient(blobName);
                BlockBlobOpenWriteOptions blobOptions = new()
                {
                    HttpHeaders = new()
                    {
                        ContentType = "application/json"
                    }
                };

                using Stream stream = await blobClient.OpenWriteAsync(true, blobOptions, cancellationToken);
                await stream.WriteAsync(bundleBytes, cancellationToken);
            }

            _logger.LogInformation("Completed uploading {} expanded patient bundles", patientResources.Count);
        }

        private bool IsAggregateMeasureReport(MeasureReport measureReport)
        {
            // Aggregate measure reports typically have type = "summary" and no subject
            return measureReport.Type == MeasureReport.MeasureReportType.Summary &&
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

        private HashSet<(string ResourceType, string ResourceId)> FindReferencesInResources(List<Resource> resources)
        {
            var references = new HashSet<(string, string)>();
            var jsonOptions = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);

            foreach (var resource in resources)
            {
                try
                {
                    // Serialize to JSON and parse to find reference patterns
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
