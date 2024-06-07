using Azure.Storage.Blobs;
using LantanaGroup.Link.Submission.Application.Interfaces;
using LantanaGroup.Link.Submission.Settings;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Submission.Application.Repositories;

public class BlobStorageRepository : IBlobStorageRepository
{
    private readonly ILogger<BlobStorageRepository> _logger;
    private readonly BlobStorageSettings _blobStorageSettings;

    public BlobStorageRepository(IOptions<BlobStorageSettings> blobStorageSettings, ILogger<BlobStorageRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _blobStorageSettings = blobStorageSettings.Value ?? throw new ArgumentNullException(nameof(blobStorageSettings));
    }

    public Task DeleteBlobAsync(string blobName)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> DownloadBlobAsync(string blobName)
    {
        throw new NotImplementedException();
    }

    public async Task UploadBlobAsync(string directoryName, string blobName, string blob)
    {
        BlobContainerClient containerClient = new BlobContainerClient(_blobStorageSettings.ConnectionString, _blobStorageSettings.ContainerName);
        containerClient.CreateIfNotExists();
        BlobClient blobClient = containerClient.GetBlobClient($"{directoryName}/{blobName}");

        await blobClient.UploadAsync(BinaryData.FromString(blob), overwrite: true);
    }


}
