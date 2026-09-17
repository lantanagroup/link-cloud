using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using System.Text;

namespace Automation.UI.Services.Persistence;

public sealed class AzureBlobSnapshotPayloadStore : ISnapshotPayloadStore
{
    private readonly ImportedBundleBlobStorageSettings _settings;
    private readonly BlobContainerClient _container;
    private readonly HashSet<string> _externalizedDomains;
    private readonly Func<string, CancellationToken, AsyncPageable<BlobItem>> _listBlobs;
    private readonly Func<string, CancellationToken, Task> _deleteBlob;

    public AzureBlobSnapshotPayloadStore(IOptions<ImportedBundleBlobStorageSettings> settings)
        : this(
            settings.Value,
            CreateContainer(settings.Value),
            listBlobs: null,
            deleteBlob: null)
    {
    }

    internal AzureBlobSnapshotPayloadStore(
        ImportedBundleBlobStorageSettings settings,
        BlobContainerClient container,
        Func<string, CancellationToken, AsyncPageable<BlobItem>>? listBlobs,
        Func<string, CancellationToken, Task>? deleteBlob)
    {
        _settings = settings;
        _container = container;
        _externalizedDomains = (_settings.SnapshotPayloadExternalizedDomains ?? [])
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _listBlobs = listBlobs ?? ((prefix, ct) => _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, ct));
        _deleteBlob = deleteBlob ?? ((blobName, ct) =>
            _container.DeleteBlobIfExistsAsync(blobName, DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct));
    }

    public bool ShouldExternalize(string domain, int payloadUtf8Bytes)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return false;

        if (payloadUtf8Bytes <= 0)
            return false;

        if (!_externalizedDomains.Contains(domain))
            return false;

        var maxInlineBytes = _settings.SnapshotPayloadInlineMaxBytes;
        if (maxInlineBytes <= 0)
            return true;

        return payloadUtf8Bytes > maxInlineBytes;
    }

    public async Task<SnapshotPayloadPointer> StoreAsync(Guid runId, string domain, string payloadJson, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new InvalidOperationException("Snapshot payload JSON is required.");

        await _container.CreateIfNotExistsAsync(cancellationToken: ct);

        var blobName = BuildBlobName(runId, domain);
        var bytes = Encoding.UTF8.GetBytes(payloadJson);

        var blob = _container.GetBlobClient(blobName);
        using var stream = new MemoryStream(bytes, writable: false);
        var upload = await blob.UploadAsync(stream, overwrite: true, cancellationToken: ct);
        await blob.SetHttpHeadersAsync(new BlobHttpHeaders
        {
            ContentType = "application/json"
        }, cancellationToken: ct);

        return new SnapshotPayloadPointer
        {
            BlobName = blobName,
            Utf8Bytes = bytes.Length,
            ETag = upload.Value.ETag.ToString()
        };
    }

    public async Task<string?> ReadAsync(SnapshotPayloadPointer pointer, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pointer.BlobName))
            return null;

        var blob = _container.GetBlobClient(pointer.BlobName);
        var exists = await blob.ExistsAsync(ct);
        if (!exists.Value)
            return null;

        var download = await blob.DownloadContentAsync(ct);
        return download.Value.Content.ToString();
    }

    public async Task DeleteIfExistsAsync(SnapshotPayloadPointer pointer, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pointer.BlobName))
            return;

        await _container.DeleteBlobIfExistsAsync(pointer.BlobName, cancellationToken: ct);
    }

    public async Task DeleteRunPayloadsAsync(Guid runId, CancellationToken ct = default)
    {
        var prefix = BuildRunPrefix(runId);

        try
        {
            await foreach (var blob in _listBlobs(prefix, ct))
            {
                await _deleteBlob(blob.Name, ct);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Missing container is treated as an empty cleanup result.
        }
    }

    private static BlobContainerClient CreateContainer(ImportedBundleBlobStorageSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            throw new InvalidOperationException("InternalBlobStorage:ConnectionString is required for snapshot payload storage.");
        if (string.IsNullOrWhiteSpace(settings.BlobContainerName))
            throw new InvalidOperationException("InternalBlobStorage:BlobContainerName is required for snapshot payload storage.");

        return new BlobContainerClient(settings.ConnectionString, settings.BlobContainerName);
    }

    private string BuildBlobName(Guid runId, string domain)
    {
        var sanitizedDomain = string.Join("-", domain
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-'));

        return $"{BuildRunPrefix(runId)}{sanitizedDomain}/{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json";
    }

    private string BuildRunPrefix(Guid runId)
    {
        var root = string.IsNullOrWhiteSpace(_settings.SnapshotPayloadBlobRoot)
            ? "automation/run-snapshots"
            : _settings.SnapshotPayloadBlobRoot.Trim('/');

        return $"{root}/{runId:N}/";
    }
}
