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
}
