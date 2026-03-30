namespace LantanaGroup.Link.Sdk.Clients;

public interface IValidationServiceClient
{
    Task InitializeArtifactsAsync(CancellationToken cancellationToken = default);
    Task InitializeCategoriesAsync(CancellationToken cancellationToken = default);
    Task UpsertResourceArtifactAsync(string artifactId, string resourceJson, CancellationToken cancellationToken = default);
    Task<string?> GetValidationResultsAsync(string facilityId, string reportId, string severity = "WARNING", CancellationToken cancellationToken = default);
}
