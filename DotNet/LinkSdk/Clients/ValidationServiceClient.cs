using System.Net.Http.Headers;
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

    public Task<LinkApiResponse<ValidationCategoryApiModel>> GetCategoryAsync(string id, CancellationToken cancellationToken = default) =>
        SendAsync<ValidationCategoryApiModel>(() => Request($"validation/category/{Uri.EscapeDataString(id)}")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> UpdateCategoryAsync(ValidationCategoryApiModel category, CancellationToken cancellationToken = default)
    {
        // The validation service is Java. Its Jackson binding is case-sensitive, so the body uses
        // the camelCase names the Category entity declares.
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            id = category.Id,
            title = category.Title,
            severity = category.Severity,
            acceptable = category.Acceptable,
            submit = category.Submit,
            review = category.Review,
            guidance = category.Guidance
        });
        return SendAsync(() => Request($"validation/category/{Uri.EscapeDataString(category.Id)}")
            .WithHeader("Content-Type", "application/json")
            .PutStringAsync(json, cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse> UpsertResourceArtifactAsync(string artifactId, string resourceJson, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"validation/artifact/RESOURCE/{artifactId}")
            .WithHeader("Content-Type", "application/json")
            .PutStringAsync(resourceJson, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> UploadPackageAsync(string packageName, byte[] content, CancellationToken cancellationToken = default)
    {
        var payload = new ByteArrayContent(content);
        payload.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return SendAsync(() => Request($"validation/artifact/PACKAGE/{Uri.EscapeDataString(packageName)}")
            .PutAsync(payload, cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse<string>> GetPackageDetailsAsync(string packageName, CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request($"validation/artifact/PACKAGE/{Uri.EscapeDataString(packageName)}")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> GetPackageDependenciesAsync(string packageName, CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request($"validation/artifact/PACKAGE/{Uri.EscapeDataString(packageName)}/tx-dependencies")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> GetAllDependenciesAsync(CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request("validation/artifact/tx-dependencies")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> ExportCategoriesAsync(CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request("validation/category/$bulk-export")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> ImportCategoriesAsync(string snapshotsJson, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("validation/category/$bulk-import")
            .WithHeader("Content-Type", "application/json")
            .PostStringAsync(snapshotsJson, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> GetCategoryRuleHistoryAsync(string id, CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request($"validation/category/{Uri.EscapeDataString(id)}/rule/history")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> SaveCategoryRuleAsync(string id, string matcherJson, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"validation/category/{Uri.EscapeDataString(id)}/rule")
            .WithHeader("Content-Type", "application/json")
            .PutStringAsync(matcherJson, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteCategoryRuleAsync(long ruleId, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"validation/category/{ruleId}")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> GetValidationResultsAsync(string facilityId, string reportId, string severity = "WARNING", CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request($"validation/result/{facilityId}/{reportId}").SetQueryParam("severity", severity).GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> GetValidationResultSummaryAsync(string facilityId, string reportId, string severity = "WARNING", CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request($"validation/result-summaries/{facilityId}/{reportId}").SetQueryParam("severity", severity).GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> CategorizeResultsAsync(string resultsJson, bool summarize = true, CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request("validation/$categorize")
            .SetQueryParam("summarize", summarize ? "true" : "false")
            .WithHeader("Content-Type", "application/json")
            .PostStringAsync(resultsJson, cancellationToken: cancellationToken));
}
