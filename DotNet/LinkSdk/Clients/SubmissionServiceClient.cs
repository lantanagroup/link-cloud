using System.Net.Http;
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

    /// <summary>Sends this client's calls through Admin.BFF with the same relative paths.</summary>
    public SubmissionServiceClient(
        AdminBffRoute route,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(route, bearerOptions, tokenServiceSettings, tokenService)
    { }

    public Task<LinkApiResponse<byte[]>> DownloadSubmissionAsync(
        string facilityId,
        string reportId,
        bool external = true,
        CancellationToken cancellationToken = default) =>
        SendBytesAsync(() => Request($"Submission/{facilityId}/{reportId}")
            .SetQueryParam("external", external.ToString().ToLowerInvariant())
            .GetAsync(cancellationToken: cancellationToken));

    public async Task<SubmissionDownloadResult> CopySubmissionAsync(
        string facilityId,
        string reportId,
        Stream destination,
        bool external = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        try
        {
            // ResponseHeadersRead: do not buffer the ZIP before the caller copies it.
            using var response = await Request($"Submission/{facilityId}/{reportId}")
                .SetQueryParam("external", external.ToString().ToLowerInvariant())
                .GetAsync(HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode is >= 200 and < 300)
            {
                await using var body = await response.GetStreamAsync().ConfigureAwait(false);
                await body.CopyToAsync(destination, 81920, cancellationToken).ConfigureAwait(false);
                return new SubmissionDownloadResult(response.StatusCode, null);
            }

            var error = await response.GetStringAsync().ConfigureAwait(false);
            return new SubmissionDownloadResult(response.StatusCode, error);
        }
        catch (FlurlHttpException ex)
        {
            string? error = null;
            try
            {
                error = await ex.GetResponseStringAsync().ConfigureAwait(false);
            }
            catch (IOException)
            {
                // The status is still reported when the error body cannot be read.
            }

            return new SubmissionDownloadResult(ex.StatusCode ?? 0, error);
        }
    }
}
