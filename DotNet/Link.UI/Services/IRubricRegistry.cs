using LantanaGroup.Link.Sdk.ApiClient;

namespace Link.UI.Services;

public interface IRubricRegistry
{
    Task<LinkApiResponse<string>> SendAsync(
        HttpMethod method,
        string relativePath,
        string? body,
        string? contentType,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken cancellationToken);
}
