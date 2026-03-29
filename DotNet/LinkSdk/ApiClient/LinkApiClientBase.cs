using System.Text.Json;
using System.Text.Json.Serialization;
using Flurl.Http;

namespace LantanaGroup.Link.Sdk.ApiClient;

public abstract class LinkApiClientBase
{
    private readonly IFlurlClient _client;

    protected LinkApiClientBase(ApiClientSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _client = new FlurlClient(settings.BaseUrl);
        if (!string.IsNullOrWhiteSpace(settings.BearerToken))
            _client.WithHeader("Authorization", $"Bearer {settings.BearerToken}");
    }

    protected IFlurlRequest Request(string relativePath) => _client.Request(relativePath);

    protected static async Task<T?> ReadJsonAsync<T>(IFlurlResponse response)
    {
        var body = await response.GetStringAsync();
        if (string.IsNullOrWhiteSpace(body))
            return default;

        return JsonSerializer.Deserialize<T>(body, JsonOptions);
    }

    protected static async Task<T?> ReadJsonSafeAsync<T>(IFlurlResponse response)
    {
        try
        {
            return await ReadJsonAsync<T>(response);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    protected static async Task<List<T>> ReadListOrPagedRecordsSafeAsync<T>(IFlurlResponse response)
    {
        var body = await response.GetStringAsync();
        if (string.IsNullOrWhiteSpace(body))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<T>>(root.GetRawText(), JsonOptions) ?? [];
            }

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("records", out var records) &&
                records.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<T>>(records.GetRawText(), JsonOptions) ?? [];
            }
        }
        catch (JsonException)
        {
        }

        return [];
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
