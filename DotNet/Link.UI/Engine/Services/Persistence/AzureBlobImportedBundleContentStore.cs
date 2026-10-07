using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using System.Text;

namespace Automation.UI.Services.Persistence;

public sealed class AzureBlobImportedBundleContentStore : IImportedBundleContentStore
{
    private readonly ImportedBundleBlobStorageSettings _settings;
    private readonly BlobContainerClient _container;

    public AzureBlobImportedBundleContentStore(IOptions<ImportedBundleBlobStorageSettings> settings)
    {
        _settings = settings.Value;

        if (string.IsNullOrWhiteSpace(_settings.ConnectionString))
            throw new InvalidOperationException("InternalBlobStorage:ConnectionString is required for imported bundle storage.");
        if (string.IsNullOrWhiteSpace(_settings.BlobContainerName))
            throw new InvalidOperationException("InternalBlobStorage:BlobContainerName is required for imported bundle storage.");

        _container = new BlobContainerClient(_settings.ConnectionString, _settings.BlobContainerName);
    }

    public async Task<StoredImportedBundleContent> StoreAsync(Guid bundleId, string contentHash, string bundleJson, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bundleJson))
            throw new InvalidOperationException("Bundle JSON is required.");

        await _container.CreateIfNotExistsAsync(cancellationToken: ct);

        var blobName = BuildBlobName(bundleId, contentHash);
        var bytes = Encoding.UTF8.GetBytes(bundleJson);
        var blob = _container.GetBlobClient(blobName);

        var exists = await blob.ExistsAsync(ct);
        if (exists.Value)
        {
            var props = await blob.GetPropertiesAsync(cancellationToken: ct);
            return new StoredImportedBundleContent(blobName, props.Value.ContentLength);
        }

        using var stream = new MemoryStream(bytes, writable: false);
        await blob.UploadAsync(stream, overwrite: true, cancellationToken: ct);
        await blob.SetHttpHeadersAsync(new BlobHttpHeaders
        {
            ContentType = "application/fhir+json"
        }, cancellationToken: ct);

        return new StoredImportedBundleContent(blobName, bytes.LongLength);
    }

    public async Task<string?> ReadAsync(ImportedBundleDocument bundle, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(bundle.BundleBlobName))
        {
            var blob = _container.GetBlobClient(bundle.BundleBlobName);
            var exists = await blob.ExistsAsync(ct);
            if (!exists.Value)
                return null;

            var download = await blob.DownloadContentAsync(ct);
            return download.Value.Content.ToString();
        }

        return bundle.BundleJson;
    }

    public async Task DeleteAsync(ImportedBundleDocument bundle, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bundle.BundleBlobName))
            return;

        await _container.DeleteBlobIfExistsAsync(bundle.BundleBlobName, cancellationToken: ct);
    }

    private string BuildBlobName(Guid bundleId, string contentHash)
    {
        var root = string.IsNullOrWhiteSpace(_settings.BlobRoot)
            ? "automation/imported-bundles"
            : _settings.BlobRoot.Trim('/');

        return $"{root}/{bundleId:N}-{contentHash}.json";
    }
}
