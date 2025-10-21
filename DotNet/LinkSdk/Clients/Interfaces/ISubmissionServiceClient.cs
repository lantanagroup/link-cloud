using LantanaGroup.Link.Sdk.ApiClient;

namespace LantanaGroup.Link.Sdk.Clients;

public interface ISubmissionServiceClient
{
    Task<LinkApiResponse<byte[]>> DownloadSubmissionAsync(string facilityId, string reportId, bool external = true, CancellationToken cancellationToken = default);
}
