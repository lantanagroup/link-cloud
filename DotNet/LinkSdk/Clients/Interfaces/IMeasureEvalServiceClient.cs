using LantanaGroup.Link.Sdk.ApiClient;

namespace LantanaGroup.Link.Sdk.Clients;

public interface IMeasureEvalServiceClient
{
    Task<LinkApiResponse> PutMeasureDefinitionAsync(string bundleJson, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<string>> GetMeasureDefinitionAsync(string measureId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<string>> GetAllMeasureDefinitionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Related artifacts named by one measure definition:
    /// <c>GET /api/measureeval/measure-definition/{id}/relatedArtifact</c>.
    /// </summary>
    Task<LinkApiResponse<string>> GetRelatedArtifactsAsync(string measureId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The patient bundle MeasureEval used as input for one report:
    /// <c>GET /api/measureeval/patient/{facilityId}/{reportId}/{patientId}</c>.
    /// </summary>
    Task<LinkApiResponse<string>> GetPatientBundleAsync(string facilityId, string reportId, string patientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// CQL for one library in a measure definition:
    /// <c>GET /api/measureeval/measure-definition/{id}/{libraryId}/$cql</c>.
    /// <paramref name="range"/> is optional (<c>37:1-38:22</c>).
    /// </summary>
    Task<LinkApiResponse<string>> GetMeasureCqlAsync(string measureId, string libraryId, string? range = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates a measure against a FHIR Parameters resource:
    /// <c>POST /api/measureeval/measure-definition/{id}/$evaluate</c>.
    /// </summary>
    Task<LinkApiResponse<string>> EvaluateMeasureAsync(string measureId, string parametersJson, string? debug = null, CancellationToken cancellationToken = default);
}
