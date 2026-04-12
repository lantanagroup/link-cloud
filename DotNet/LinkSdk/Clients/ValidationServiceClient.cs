using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

public class ValidationServiceClient : LinkApiClientBase, IValidationServiceClient
{
    public ValidationServiceClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(
            serviceRegistry.Value.ValidationServiceApiUrl
                ?? throw new InvalidOperationException("Validation service URL is not configured in ServiceRegistry."),
            bearerOptions, tokenServiceSettings, tokenService)
    { }

    public Task InitializeArtifactsAsync(CancellationToken cancellationToken = default) =>
        Request("validation/artifact/$initialize").PostAsync(cancellationToken: cancellationToken);

    public Task InitializeCategoriesAsync(CancellationToken cancellationToken = default) =>
        Request("validation/category/$initialize").PostAsync(cancellationToken: cancellationToken);

    public async Task<bool> HasArtifactsAsync(CancellationToken cancellationToken = default)
    {
        var artifacts = await Request("validation/artifact")
            .GetJsonAsync<List<object>>(cancellationToken: cancellationToken);
        return artifacts is { Count: > 0 };
    }

    public async Task<bool> HasCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await Request("validation/category")
            .GetJsonAsync<List<object>>(cancellationToken: cancellationToken);
        return categories is { Count: > 0 };
    }

    public Task UpsertResourceArtifactAsync(string artifactId, string resourceJson, CancellationToken cancellationToken = default) =>
        Request($"validation/artifact/RESOURCE/{artifactId}").PutStringAsync(resourceJson, cancellationToken: cancellationToken);

    public Task<string?> GetValidationResultsAsync(string facilityId, string reportId, string severity = "WARNING", CancellationToken cancellationToken = default) =>
        GetOrDefaultAsync(() => Request($"validation/result/{facilityId}/{reportId}").SetQueryParam("severity", severity).GetStringAsync(cancellationToken: cancellationToken));
}
