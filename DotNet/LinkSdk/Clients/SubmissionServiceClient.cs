using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

public class SubmissionServiceClient : LinkApiClientBase, ISubmissionServiceClient
{
    public SubmissionServiceClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(
            serviceRegistry.Value.SubmissionServiceApiUrl
                ?? throw new InvalidOperationException("Submission service URL is not configured in ServiceRegistry."),
            bearerOptions, tokenServiceSettings, tokenService)
    { }

    public async Task<(byte[] Bytes, string? ContentType)> DownloadSubmissionAsync(
        string facilityId,
        string reportId,
        bool external = true,
        CancellationToken cancellationToken = default)
    {
        var response = await Request($"Submission/{facilityId}/{reportId}")
            .SetQueryParam("external", external.ToString().ToLowerInvariant())
            .GetAsync(cancellationToken: cancellationToken);

        return (await response.GetBytesAsync(), response.ResponseMessage.Content.Headers.ContentType?.MediaType);
    }
}
