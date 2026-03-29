using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using System.Net;

namespace LantanaGroup.Link.Sdk.Clients;

public sealed class ValidationServiceClient : LinkApiClientBase
{
    public ValidationServiceClient(ApiClientSettings settings)
        : base(settings)
    {
    }

    public async Task<HttpStatusCode> InitializeArtifactsAsync(CancellationToken cancellationToken = default)
    {
        var response = await Request("validation/artifact/$initialize")
            .AllowAnyHttpStatus()
            .PostAsync(cancellationToken: cancellationToken);

        return response.ResponseMessage.StatusCode;
    }

    public async Task<HttpStatusCode> InitializeCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var response = await Request("validation/category/$initialize")
            .AllowAnyHttpStatus()
            .PostAsync(cancellationToken: cancellationToken);

        return response.ResponseMessage.StatusCode;
    }

    public async Task<HttpStatusCode> GetValidationResultsAsync(string facilityId, string reportId, string severity = "WARNING", CancellationToken cancellationToken = default)
    {
        var response = await Request($"validation/result/{facilityId}/{reportId}")
            .SetQueryParam("severity", severity)
            .AllowAnyHttpStatus()
            .GetAsync(cancellationToken: cancellationToken);

        return response.ResponseMessage.StatusCode;
    }
}
