using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Submission.Application.Config;
using LantanaGroup.Link.Submission.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Submission.Application.Services
{
    public class BlobStorageService : IStorageService
    {
        private readonly ILogger<BlobStorageService> _logger;
        private readonly InternalBlobStorageSettings _internalSettings;
        private readonly ExternalBlobStorageSettings _externalSettings;
        private readonly BlobContainerClient? _internalContainerClient;
        private readonly BlobContainerClient? _externalContainerClient;

        public string DestinationType => "azure_blob_storage";

        private static BlobContainerClient? GetContainerClient(BlobStorageSettings settings)
        {
            if (settings.ConnectionString == null)
            {
                return null;
            }
            return new BlobContainerClient(settings.ConnectionString, settings.BlobContainerName);
        }

        internal static string GetExternalBlobName(string? blobRoot, string? measurePrefix, string reportName, string bundleName, bool flatten) =>
            flatten
                ? GetBlobName(blobRoot, measurePrefix, $"{reportName}_{bundleName}")
                : GetBlobName(blobRoot, measurePrefix, reportName, bundleName);

        private static string GetBlobName(string? blobRoot, string? measurePrefix, params string[] segments)
        {
            IEnumerable<string> enumerable = segments;
            if (!string.IsNullOrEmpty(measurePrefix))
            {
                enumerable = enumerable.Prepend(measurePrefix);
            }
            if (!string.IsNullOrEmpty(blobRoot))
            {
                enumerable = enumerable.Prepend(blobRoot);
            }
            return string.Join('/', enumerable.Select(component => component.Trim('/')));
        }

        public BlobStorageService(
            ILogger<BlobStorageService> logger,
            IOptions<InternalBlobStorageSettings> internalSettings,
            IOptions<ExternalBlobStorageSettings> externalSettings)
        {
            _logger = logger;
            _internalSettings = internalSettings.Value;
            _externalSettings = externalSettings.Value;
            _internalContainerClient = GetContainerClient(_internalSettings);
            _externalContainerClient = GetContainerClient(_externalSettings);
        }

        private string ChangeBlobRoot(string? measurePrefix, string blobName)
        {
            if (_internalSettings.BlobRoot != null && blobName.StartsWith(_internalSettings.BlobRoot))
            {
                blobName = blobName.Substring(_internalSettings.BlobRoot.Length);
            }
            if (_externalSettings.FlattenHierarchy)
            {
                int index = blobName.LastIndexOf('/');
                if (index != -1)
                {
                    blobName = $"{blobName[..index]}_{blobName[(index + 1)..]}";
                }
            }
            return GetBlobName(_externalSettings.BlobRoot, measurePrefix, blobName);
        }

        private string? GetMeasurePrefix(ICollection<string> reportTypes)
        {
            if (!_externalSettings.UseMeasurePrefix)
            {
                return null;
            }
            if (_externalSettings.MeasurePrefixesByReportType == null)
            {
                return reportTypes.FirstOrDefault();
            }
            foreach (string reportType in reportTypes)
            {
                if (_externalSettings.MeasurePrefixesByReportType.TryGetValue(reportType, out string? measurePrefix))
                {
                    return measurePrefix;
                }
            }
            return reportTypes.FirstOrDefault();
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
            string? measurePrefix = GetMeasurePrefix(value.ReportTypes);
            string blobName;
            if (string.IsNullOrEmpty(value.PayloadUri))
            {
                string reportName = ReportHelpers.GetReportName(key.ReportScheduleId, key.FacilityId, value.ReportTypes, value.StartDate);
                string bundleName = value.PayloadType switch
                {
                    PayloadType.MeasureReportSubmissionEntry => $"patient-{value.PatientId}.ndjson",
                    PayloadType.ReportSchedule => "manifest.ndjson",
                    _ => $"{Guid.NewGuid()}.ndjson"
                };
                blobName = GetExternalBlobName(_externalSettings.BlobRoot, measurePrefix, reportName, bundleName, _externalSettings.FlattenHierarchy);
            }
            else
            {
                BlobUriBuilder uriBuilder = new(new Uri(value.PayloadUri));
                blobName = ChangeBlobRoot(measurePrefix, uriBuilder.BlobName);
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

        public Task<IDictionary<string, byte[]>> DownloadFromExternalAsync(ICollection<string> reportTypes, string payloadRootUri, CancellationToken cancellationToken = default)
        {
            if (!HasExternalClient())
            {
                throw new InvalidOperationException("Not configured for external blob storage.");
            }
            string? measurePrefix = GetMeasurePrefix(reportTypes);
            BlobUriBuilder uriBuilder = new(new Uri(payloadRootUri));
            string prefix = ChangeBlobRoot(measurePrefix, uriBuilder.BlobName);
            return DownloadAsync(_externalContainerClient, prefix, cancellationToken);
        }
    }
}
