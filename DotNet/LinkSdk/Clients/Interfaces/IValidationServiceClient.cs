using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Models.Integration.Validation;

namespace LantanaGroup.Link.Sdk.Clients;

public interface IValidationServiceClient
{
    Task<LinkApiResponse<List<ValidationArtifactApiModel>>> GetArtifactsAsync(CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<ValidationCategoryApiModel>>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<LinkApiResponse<ValidationCategoryApiModel>> GetCategoryAsync(string id, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> UpdateCategoryAsync(ValidationCategoryApiModel category, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> InitializeArtifactsAsync(CancellationToken cancellationToken = default);
    Task<LinkApiResponse> InitializeCategoriesAsync(CancellationToken cancellationToken = default);
    Task<LinkApiResponse> UpsertResourceArtifactAsync(string artifactId, string resourceJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces one package: <c>PUT /api/validation/artifact/PACKAGE/{name}</c> as
    /// <c>application/octet-stream</c>.
    /// </summary>
    Task<LinkApiResponse> UploadPackageAsync(string packageName, byte[] content, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/validation/artifact/PACKAGE/{name}</c>.</summary>
    Task<LinkApiResponse<string>> GetPackageDetailsAsync(string packageName, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/validation/artifact/PACKAGE/{name}/tx-dependencies</c>.</summary>
    Task<LinkApiResponse<string>> GetPackageDependenciesAsync(string packageName, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/validation/artifact/tx-dependencies</c>.</summary>
    Task<LinkApiResponse<string>> GetAllDependenciesAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/validation/category/$bulk-export</c>.</summary>
    Task<LinkApiResponse<string>> ExportCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the category catalog: <c>POST /api/validation/category/$bulk-import</c>.
    /// Categories absent from <paramref name="snapshotsJson"/> are deleted.
    /// </summary>
    Task<LinkApiResponse> ImportCategoriesAsync(string snapshotsJson, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/validation/category/{id}/rule/history</c>.</summary>
    Task<LinkApiResponse<string>> GetCategoryRuleHistoryAsync(string id, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /api/validation/category/{id}/rule</c> with a matcher object.</summary>
    Task<LinkApiResponse> SaveCategoryRuleAsync(string id, string matcherJson, CancellationToken cancellationToken = default);

    /// <summary>Deletes one rule by its numeric id: <c>DELETE /api/validation/category/{ruleId}</c>.</summary>
    Task<LinkApiResponse> DeleteCategoryRuleAsync(long ruleId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<string>> GetValidationResultsAsync(string facilityId, string reportId, string severity = "WARNING", CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts results at or above <paramref name="severity"/>. The body is a small summary, not the result list.
    /// </summary>
    Task<LinkApiResponse<string>> GetValidationResultSummaryAsync(string facilityId, string reportId, string severity = "WARNING", CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-applies the latest category rules. <paramref name="summarize"/> returns category counts
    /// instead of the issue list.
    /// </summary>
    Task<LinkApiResponse<string>> CategorizeResultsAsync(string resultsJson, bool summarize = true, CancellationToken cancellationToken = default);
}
