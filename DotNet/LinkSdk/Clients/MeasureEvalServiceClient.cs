using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;

namespace LantanaGroup.Link.Sdk.Clients;

public sealed class MeasureEvalServiceClient : LinkApiClientBase
{
    public MeasureEvalServiceClient(ApiClientSettings settings)
        : base(settings)
    {
    }

    public Task PutMeasureDefinitionAsync(
        string bundleJson,
        CancellationToken cancellationToken = default) =>
        Request("measureeval/measure-definition")
            .WithHeader("Content-Type", "application/json")
            .PutStringAsync(bundleJson, cancellationToken: cancellationToken);

    public Task<string> GetMeasureDefinitionAsync(
        string measureId,
        CancellationToken cancellationToken = default) =>
        Request($"measureeval/measure-definition/{measureId}")
            .GetStringAsync(cancellationToken: cancellationToken);
}
