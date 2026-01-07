using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Submission.Application.Config;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Submission.Application.Services
{
    public class BlobStorageService
    {
        private readonly ILogger<BlobStorageService> _logger;
        private readonly InternalBlobStorageSettings _internalSettings;
        private readonly ExternalBlobStorageSettings _externalSettings;
        private readonly BlobContainerClient? _internalContainerClient;
        private readonly BlobContainerClient? _externalContainerClient;

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
            IOptions<ExternalBlobStorageSettings> externalSettings)
        {
            _logger = logger;
            _internalSettings = internalSettings.Value;
            _externalSettings = externalSettings.Value;
            _internalContainerClient = GetContainerClient(_internalSettings);
            _externalContainerClient = GetContainerClient(_externalSettings);
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
                blobName = GetBlobName(_externalSettings.BlobRoot, reportName, bundleName);
            }
            else
            {
                BlobUriBuilder uriBuilder = new(new Uri(value.PayloadUri));
                blobName = ChangeBlobRoot(uriBuilder.BlobName);
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
