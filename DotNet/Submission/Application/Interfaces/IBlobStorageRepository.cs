namespace LantanaGroup.Link.Submission.Application.Interfaces;

public interface IBlobStorageRepository
{
    Task UploadBlobAsync(string directoryName, string blobName, string blob);
    Task<Stream> DownloadBlobAsync(string blobName);
    Task DeleteBlobAsync(string blobName);
}
