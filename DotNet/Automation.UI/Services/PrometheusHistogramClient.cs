using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace Automation.UI.Services;

public interface IPrometheusHistogramClient
{
    Task<double?> QueryScalarAsync(string query, DateTimeOffset evaluationTime, CancellationToken cancellationToken = default);
}

public sealed class PrometheusHistogramClient : IPrometheusHistogramClient
{
    private readonly HttpClient _http;

    public PrometheusHistogramClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<double?> QueryScalarAsync(string query, DateTimeOffset evaluationTime, CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/query?query={Uri.EscapeDataString(query)}&time={Uri.EscapeDataString(evaluationTime.UtcDateTime.ToString("o", CultureInfo.InvariantCulture))}";
        using var response = await _http.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadFromJsonAsync<PromQueryResponse>(cancellationToken);
        if (payload?.Status != "success" || payload.Data?.Result is not { Count: > 0 } results)
            return null;

        var value = results[0].Value;
        if (value is not { Count: >= 2 })
            return null;

        var raw = value[1].ValueKind == JsonValueKind.String
            ? value[1].GetString()
            : value[1].ToString();
        if (string.IsNullOrWhiteSpace(raw) || raw == "NaN" || raw == "+Inf" || raw == "-Inf")
            return null;

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private sealed class PromQueryResponse
    {
        public string? Status { get; set; }
        public PromQueryData? Data { get; set; }
    }

    private sealed class PromQueryData
    {
        public List<PromQueryResult>? Result { get; set; }
    }

    private sealed class PromQueryResult
    {
        public List<JsonElement>? Value { get; set; }
    }
}
