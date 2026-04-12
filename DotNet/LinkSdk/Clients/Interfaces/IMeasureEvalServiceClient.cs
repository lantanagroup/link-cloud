namespace LantanaGroup.Link.Sdk.Clients;

public interface IMeasureEvalServiceClient
{
    Task PutMeasureDefinitionAsync(string bundleJson, CancellationToken cancellationToken = default);
    Task<string?> GetMeasureDefinitionAsync(string measureId, CancellationToken cancellationToken = default);
}
