using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

public class MeasureEvalServiceClient : LinkApiClientBase, IMeasureEvalServiceClient
{
    public MeasureEvalServiceClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(
            serviceRegistry.Value.MeasureServiceApiUrl
                ?? throw new InvalidOperationException("MeasureEval service URL is not configured in ServiceRegistry."),
            bearerOptions, tokenServiceSettings, tokenService)
    { }

    /// <summary>Sends this client's calls through Admin.BFF with the same relative paths.</summary>
    public MeasureEvalServiceClient(
        AdminBffRoute route,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(route, bearerOptions, tokenServiceSettings, tokenService)
    { }

    public Task<LinkApiResponse> PutMeasureDefinitionAsync(string bundleJson, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("measureeval/measure-definition").WithHeader("Content-Type", "application/json").PutStringAsync(bundleJson, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> GetMeasureDefinitionAsync(string measureId, CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request($"measureeval/measure-definition/{measureId}").GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> GetAllMeasureDefinitionsAsync(CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request("measureeval/measure-definition").GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> GetRelatedArtifactsAsync(string measureId, CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request($"measureeval/measure-definition/{Uri.EscapeDataString(measureId)}/relatedArtifact")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> GetPatientBundleAsync(string facilityId, string reportId, string patientId, CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request($"measureeval/patient/{Uri.EscapeDataString(facilityId)}/{Uri.EscapeDataString(reportId)}/{Uri.EscapeDataString(patientId)}")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> GetMeasureCqlAsync(string measureId, string libraryId, string? range = null, CancellationToken cancellationToken = default)
    {
        var request = Request($"measureeval/measure-definition/{Uri.EscapeDataString(measureId)}/{Uri.EscapeDataString(libraryId)}/$cql");
        if (!string.IsNullOrWhiteSpace(range))
            request = request.SetQueryParam("range", range);
        return SendStringAsync(() => request.GetAsync(cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse<string>> EvaluateMeasureAsync(string measureId, string parametersJson, string? debug = null, CancellationToken cancellationToken = default)
    {
        var request = Request($"measureeval/measure-definition/{Uri.EscapeDataString(measureId)}/$evaluate")
            .WithHeader("Content-Type", "application/json");
        if (!string.IsNullOrWhiteSpace(debug))
            request = request.SetQueryParam("debug", debug);
        return SendStringAsync(() => request.PostStringAsync(parametersJson, cancellationToken: cancellationToken));
    }
}
