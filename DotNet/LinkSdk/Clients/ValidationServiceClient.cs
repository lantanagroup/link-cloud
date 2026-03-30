using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;

namespace LantanaGroup.Link.Sdk.Clients;

public sealed class ValidationServiceClient : LinkApiClientBase
{
    public ValidationServiceClient(ApiClientSettings settings)
        : base(settings)
    {
    }

    public Task InitializeArtifactsAsync(CancellationToken cancellationToken = default) =>
        Request("validation/artifact/$initialize")
            .PostAsync(cancellationToken: cancellationToken);

    public Task InitializeCategoriesAsync(CancellationToken cancellationToken = default) =>
        Request("validation/category/$initialize")
            .PostAsync(cancellationToken: cancellationToken);

    public Task UpsertResourceArtifactAsync(
        string artifactId,
        string resourceJson,
        CancellationToken cancellationToken = default) =>
        Request($"validation/artifact/RESOURCE/{artifactId}")
            .PutStringAsync(resourceJson, cancellationToken: cancellationToken);

    public Task<string> GetValidationResultsAsync(
        string facilityId,
        string reportId,
        string severity = "WARNING",
        CancellationToken cancellationToken = default) =>
        Request($"validation/result/{facilityId}/{reportId}")
            .SetQueryParam("severity", severity)
            .GetStringAsync(cancellationToken: cancellationToken);
}
