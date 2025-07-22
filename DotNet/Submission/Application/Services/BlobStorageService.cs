using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
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
        private readonly PathNamingService _pathNamingService;

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
            PathNamingService pathNamingService)
        {
            _logger = logger;
            _internalSettings = internalSettings.Value;
            _externalSettings = externalSettings.Value;
            _internalContainerClient = GetContainerClient(_internalSettings);
            _externalContainerClient = GetContainerClient(_externalSettings);
            _pathNamingService = pathNamingService;
        }

        public bool CanDownloadFromInternal()
        {
            return _internalContainerClient != null;
        }

        public async Task<byte[]> DownloadFromInternalAsync(
            SubmitPayloadValue value,
            CancellationToken cancellationToken = default)
        {
            if (!CanDownloadFromInternal())
            {
                throw new InvalidOperationException("Not configured to download from internal blob storage.");
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

        public bool CanUploadToExternal()
        {
            return _externalContainerClient != null;
        }

        public async Task UploadToExternalAsync(
            SubmitPayloadValue value,
            byte[] content,
            CancellationToken cancellationToken = default)
        {
            if (!CanUploadToExternal())
            {
                throw new InvalidOperationException("Not configured to upload to external blob storage.");
            }
            string measurePart = _pathNamingService.GetMeasuresShortName(value.MeasureIds);
            string patientPart = value.PayloadUri.Split('/').Last();
            string blobName = GetBlobName(_externalSettings.BlobRoot, measurePart, patientPart);
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
    }
}
