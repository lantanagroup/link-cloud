using LantanaGroup.Link.Sdk.ApiClient;

namespace LantanaGroup.Link.Sdk.Clients;

public interface IMeasureEvalServiceClient
{
    Task<LinkApiResponse> PutMeasureDefinitionAsync(string bundleJson, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<string>> GetMeasureDefinitionAsync(string measureId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<string>> GetAllMeasureDefinitionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The patient bundle MeasureEval used as input for one report:
    /// <c>GET /api/measureeval/patient/{facilityId}/{reportId}/{patientId}</c>.
    /// </summary>
    Task<LinkApiResponse<string>> GetPatientBundleAsync(string facilityId, string reportId, string patientId, CancellationToken cancellationToken = default);
}
