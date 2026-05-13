namespace LantanaGroup.Link.Sdk.Clients;

public interface ISubmissionServiceClient
{
    Task<(byte[] Bytes, string? ContentType)> DownloadSubmissionAsync(string facilityId, string reportId, bool external = true, CancellationToken cancellationToken = default);
}
