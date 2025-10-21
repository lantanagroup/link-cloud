using LantanaGroup.Link.Sdk.ApiClient;

namespace LantanaGroup.Link.Sdk.Clients;

public interface IMeasureEvalServiceClient
{
    Task<LinkApiResponse> PutMeasureDefinitionAsync(string bundleJson, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<string>> GetMeasureDefinitionAsync(string measureId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<string>> GetAllMeasureDefinitionsAsync(CancellationToken cancellationToken = default);
}
