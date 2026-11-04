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

    public Task<LinkApiResponse<string>> GetValidationResultSummaryAsync(string facilityId, string reportId, string severity = "WARNING", CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request($"validation/result-summaries/{facilityId}/{reportId}").SetQueryParam("severity", severity).GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> GetReportCategoryCountsAsync(string facilityId,
                                                                     string reportId,
                                                                     bool? submit = null,
                                                                     CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request($"validation/facilities/{facilityId}/reports/{reportId}/category-counts")
            .SetQueryParam("submit", submit.HasValue ? (submit.Value ? "true" : "false") : null)
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> GetReportCategorySummariesAsync(
        string facilityId,
        string reportId,
        CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request($"validation/result/{facilityId}/{reportId}/category")
            .GetAsync(cancellationToken: cancellationToken));
    public Task<LinkApiResponse<string>> GetCategoryIssuesAsync(
        string facilityId, string reportId, string categoryId, int pageNumber = 1, int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(facilityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportId);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryId);
        if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber));
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        return SendStringAsync(() => Request($"validation/facilities/{Uri.EscapeDataString(facilityId)}/reports/{Uri.EscapeDataString(reportId)}/categories/{Uri.EscapeDataString(categoryId)}/issues")
            .SetQueryParam("pageNumber", pageNumber).SetQueryParam("pageSize", pageSize)
            .GetAsync(cancellationToken: cancellationToken));
    }

    public async Task<LinkApiResponse> CopyValidationResultsCsvAsync(
        Guid reportId, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite) throw new ArgumentException("Destination must be writable.", nameof(destination));
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // Read headers first and let the caller's token control long-running exports.
            using var response = await Request($"validation/reports/{reportId:D}/results.csv")
                .WithTimeout(Timeout.InfiniteTimeSpan)
                .GetAsync(System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            string? error = null;
            if (response.StatusCode is >= 200 and < 300)
            {
                await using var body = await response.GetStreamAsync().ConfigureAwait(false);
                await body.CopyToAsync(destination, 81920, cancellationToken).ConfigureAwait(false);
            }
            else error = await response.GetStringAsync().ConfigureAwait(false);
            return new LinkApiResponse { StatusCode = response.StatusCode, RawBody = error,
                ContentType = response.ResponseMessage.Content.Headers.ContentType?.ToString() };
        }
        catch (FlurlHttpException) when (cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        catch (FlurlHttpException ex) when (ex.StatusCode is not (>= 200 and < 300))
        {
            return new LinkApiResponse { StatusCode = ex.StatusCode ?? 0,
                RawBody = await ex.GetResponseStringAsync().ConfigureAwait(false) };
        }
    }
}
