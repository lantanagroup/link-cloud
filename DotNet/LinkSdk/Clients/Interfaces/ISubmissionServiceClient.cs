using LantanaGroup.Link.Sdk.ApiClient;

namespace LantanaGroup.Link.Sdk.Clients;

public interface ISubmissionServiceClient
{
    /// <summary>
    /// Buffers the submission ZIP. Health checks use this. A census download
    /// must use <see cref="CopySubmissionAsync"/> instead.
    /// </summary>
    Task<LinkApiResponse<byte[]>> DownloadSubmissionAsync(string facilityId, string reportId, bool external = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies the submission ZIP into <paramref name="destination"/> without
    /// buffering the response body. The response is read as soon as headers
    /// arrive (<see cref="System.Net.Http.HttpCompletionOption.ResponseHeadersRead"/>).
    /// </summary>
    Task<SubmissionDownloadResult> CopySubmissionAsync(
        string facilityId,
        string reportId,
        Stream destination,
        bool external = true,
        CancellationToken cancellationToken = default);
}
