using System.Net;
using Flurl.Http;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Normalization;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

// Normalization operations that don't fit LinkSdk's INormalizationServiceClient shape (see
// INormalizationRawClient), built on LinkApiClientBase — the same SDK base class LinkSdk's
// generated clients (e.g. NormalizationServiceClient) use — for base-URL resolution and
// system-token injection, rather than assembling requests against a plain HttpClient by hand.
internal sealed class NormalizationRawClient : LinkApiClientBase, INormalizationRawClient
{
    private const string ServiceName = "Normalization";

    public NormalizationRawClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<LinkTokenServiceSettings> linkTokenServiceConfig,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> linkBearerServiceOptions,
        ICreateSystemToken createSystemToken)
        : base(
            serviceRegistry.Value.NormalizationServiceApiUrl
                ?? throw new InvalidOperationException("Normalization service URL is not configured in ServiceRegistry."),
            linkBearerServiceOptions, linkTokenServiceConfig, createSystemToken)
    { }

    public async Task UpdateOperationAsync(UpdateNormalizationOperationRequestApiModel request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(() => Request("normalization/Operations")
            .PutJsonAsync(request, cancellationToken: cancellationToken));

        if (!response.IsSuccessStatusCode && response.StatusCode != (int)HttpStatusCode.NotModified)
        {
            throw new LinkServiceException(ServiceName, nameof(UpdateOperationAsync), response.StatusCode,
                response.TraceId, response.RawBody, response.RequestUrl);
        }
    }
}
