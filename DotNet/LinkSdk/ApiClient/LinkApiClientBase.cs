using Flurl.Http;
using Flurl.Http.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Sdk.ApiClient;

public abstract class LinkApiClientBase
{
    private readonly IFlurlClient _client;

    protected LinkApiClientBase(ApiClientSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _client = new FlurlClient(settings.BaseUrl);
        _client.Settings.JsonSerializer = new DefaultJsonSerializer(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        });

        if (!string.IsNullOrWhiteSpace(settings.BearerToken))
            _client.WithHeader("Authorization", $"Bearer {settings.BearerToken}");
    }

    protected IFlurlRequest Request(string relativePath) => _client.Request(relativePath);
}
