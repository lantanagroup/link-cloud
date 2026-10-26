using Flurl.Http;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

// Validation's per-patient result route isn't in LinkSdk's IValidationServiceClient (only the
// report-level read is) -- built on LinkApiClientBase, the same base LinkSdk's generated clients
// use, for base-URL resolution and system-token injection. Mirrors NormalizationRawClient.
internal sealed class ValidationRawClient : LinkApiClientBase, IValidationRawClient
{
    private const string ServiceName = "Validation";

    public ValidationRawClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<LinkTokenServiceSettings> linkTokenServiceConfig,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> linkBearerServiceOptions,
        ICreateSystemToken createSystemToken)
        : base(
            serviceRegistry.Value.ValidationServiceApiUrl
                ?? throw new InvalidOperationException("Validation service URL is not configured in ServiceRegistry."),
            linkBearerServiceOptions, linkTokenServiceConfig, createSystemToken)
    { }

    public async Task<string> GetPatientResultsAsync(string facilityId, string reportId, string patientId, string severity, CancellationToken cancellationToken = default)
    {
        var response = await SendStringAsync(() => Request($"validation/result/{facilityId}/{reportId}/{patientId}")
            .SetQueryParam("severity", severity)
            .GetAsync(cancellationToken: cancellationToken));

        if (!response.IsSuccessStatusCode)
        {
            throw new LinkServiceException(ServiceName, nameof(GetPatientResultsAsync), response.StatusCode,
                response.TraceId, response.RawBody, response.RequestUrl);
        }

        return response.Body ?? "";
    }
}
