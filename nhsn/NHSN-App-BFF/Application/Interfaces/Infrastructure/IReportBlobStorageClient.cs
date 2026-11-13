namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

// Report's AggregateReportUri/MeasureReportUri are plain blob URLs with no SAS token -- Report's
// own BlobStorageService just returns blobClient.Uri after writing via a connection-string-
// authenticated BlobContainerClient, so a bare GET against that URL only succeeds if the container
// happens to allow anonymous read. Rather than depend on that, this authenticates with the BFF's
// own read-only connection string to the same storage account Report writes to.
public interface IReportBlobStorageClient
{
    /// <summary>Downloads the blob at this URI, or null when no blob exists there.</summary>
    Task<byte[]?> DownloadAsync(string blobUri, CancellationToken cancellationToken = default);
}
