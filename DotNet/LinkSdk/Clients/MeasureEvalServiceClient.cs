using System.Text.Json;
using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using System.Net;

namespace LantanaGroup.Link.Sdk.Clients;

public sealed class MeasureEvalServiceClient : LinkApiClientBase
{
    public MeasureEvalServiceClient(ApiClientSettings settings)
        : base(settings)
    {
    }

    public async Task<HttpStatusCode> PutMeasureDefinitionAsync(string bundleJson, CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(bundleJson);

        var response = await Request("measureeval/measure-definition")
            .AllowAnyHttpStatus()
            .PutJsonAsync(doc.RootElement, cancellationToken: cancellationToken);

        return response.ResponseMessage.StatusCode;
    }

    public async Task<(HttpStatusCode StatusCode, string? ResponseBody)> GetMeasureDefinitionAsync(string measureId, CancellationToken cancellationToken = default)
    {
        var response = await Request($"measureeval/measure-definition/{measureId}")
            .AllowAnyHttpStatus()
            .GetAsync(cancellationToken: cancellationToken);

        return (response.ResponseMessage.StatusCode, await response.GetStringAsync());
    }
}
