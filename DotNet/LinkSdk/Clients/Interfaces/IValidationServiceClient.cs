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
    /// Gets a report's stored validation counts per category. Returns 404 when the report has no stored counts.
    /// </summary>
    Task<LinkApiResponse<string>> GetReportCategoryCountsAsync(string facilityId,
                                                              string reportId,
                                                              CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a report's categories with their finding counts, counted live from the saved findings. The validation
    /// service loads every finding of the report to answer it, so avoid it for very large reports.
    /// </summary>
    Task<LinkApiResponse<string>> GetReportCategorySummariesAsync(string facilityId,
                                                                 string reportId,
                                                                 CancellationToken cancellationToken = default);
}
