using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Models.Integration.Validation;

namespace LantanaGroup.Link.Sdk.Clients;

public interface IValidationServiceClient
{
    Task<LinkApiResponse<List<ValidationArtifactApiModel>>> GetArtifactsAsync(CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<ValidationCategoryApiModel>>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<LinkApiResponse> InitializeArtifactsAsync(CancellationToken cancellationToken = default);
    Task<LinkApiResponse> InitializeCategoriesAsync(CancellationToken cancellationToken = default);
    Task<LinkApiResponse> UpsertResourceArtifactAsync(string artifactId, string resourceJson, CancellationToken cancellationToken = default);
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
