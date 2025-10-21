using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.Validation;
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

    public Task<LinkApiResponse> InitializeArtifactsAsync(CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("validation/artifact/$initialize").PostAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> InitializeCategoriesAsync(CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("validation/category/$initialize").PostAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<ValidationArtifactApiModel>>> GetArtifactsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<ValidationArtifactApiModel>>(() => Request("validation/artifact")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<ValidationCategoryApiModel>>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<ValidationCategoryApiModel>>(() => Request("validation/category")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> UpsertResourceArtifactAsync(string artifactId, string resourceJson, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"validation/artifact/RESOURCE/{artifactId}")
            .WithHeader("Content-Type", "application/json")
            .PutStringAsync(resourceJson, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> GetValidationResultsAsync(string facilityId, string reportId, string severity = "WARNING", CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request($"validation/result/{facilityId}/{reportId}").SetQueryParam("severity", severity).GetAsync(cancellationToken: cancellationToken));
}
