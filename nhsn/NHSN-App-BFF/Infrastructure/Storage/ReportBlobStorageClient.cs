using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Settings;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Storage;

// See IReportBlobStorageClient -- authenticates with the BFF's own connection string to Report's
// storage account instead of requesting the bare blob URI directly. The container and blob name
// are parsed back out of the URI Report recorded rather than reconstructed, so this stays correct
// regardless of how Report names its containers or blob paths.
internal sealed class ReportBlobStorageClient : IReportBlobStorageClient
{
    private readonly ReportBlobStorageSettings _settings;

    public ReportBlobStorageClient(IOptions<ReportBlobStorageSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task<byte[]?> DownloadAsync(string blobUri, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ConnectionString))
        {
            throw new InvalidOperationException("Report blob storage connection string is not configured.");
        }

        var uriBuilder = new BlobUriBuilder(new Uri(blobUri));
        var blobClient = new BlobClient(_settings.ConnectionString, uriBuilder.BlobContainerName, uriBuilder.BlobName);

        try
        {
            var download = await blobClient.DownloadContentAsync(cancellationToken);
            return download.Value.Content.ToArray();
        }
        catch (RequestFailedException ex) when (ex.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
    }
}
