using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Automation.UI.Services;

public sealed record PromSample(
    string ExportedJob,
    double Value,
    IReadOnlyDictionary<string, string>? Labels = null);

public interface IPrometheusHistogramClient
{
    Task<bool> IsReachableAsync(CancellationToken cancellationToken = default);
    Task<double?> QueryScalarAsync(string query, DateTimeOffset evaluationTime, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PromSample>> QueryVectorAsync(string query, DateTimeOffset? evaluationTime = null, CancellationToken cancellationToken = default);
}

public sealed class PrometheusHistogramClient : IPrometheusHistogramClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<PrometheusHistogramClient> _logger;

    public PrometheusHistogramClient(HttpClient http, ILogger<PrometheusHistogramClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Compose DNS name <c>prometheus</c> does not resolve on the Windows host; use localhost.
    /// </summary>
    public static string? ResolveQueryEndpoint(string? endpoint, Func<string, bool>? hostExists = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return endpoint;

        if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri))
            return endpoint.Trim();

        if (!string.Equals(uri.Host, "prometheus", StringComparison.OrdinalIgnoreCase))
            return uri.ToString().TrimEnd('/');

        hostExists ??= HostExists;
        if (hostExists(uri.Host))
            return uri.ToString().TrimEnd('/');

        return new UriBuilder(uri) { Host = "localhost" }.Uri.ToString().TrimEnd('/');
    }

    private static bool HostExists(string host)
    {
        try
        {
            _ = Dns.GetHostAddresses(host);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    public async Task<bool> IsReachableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync("/api/v1/status/buildinfo", cancellationToken);
            if (response.IsSuccessStatusCode)
                return true;

            _logger.LogWarning(
                "Prometheus at {BaseAddress} returned {StatusCode} for buildinfo.",
                _http.BaseAddress,
                (int)response.StatusCode);
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Prometheus is not reachable at {BaseAddress}.", _http.BaseAddress);
            return false;
        }
    }

    public async Task<double?> QueryScalarAsync(string query, DateTimeOffset evaluationTime, CancellationToken cancellationToken = default)
    {
        var samples = await QueryVectorAsync(query, evaluationTime, cancellationToken);
        return samples.Count == 0 ? null : samples[0].Value;
    }

    public async Task<IReadOnlyList<PromSample>> QueryVectorAsync(
        string query,
        DateTimeOffset? evaluationTime = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/query?query={Uri.EscapeDataString(query)}";
        if (evaluationTime is DateTimeOffset at)
            url += $"&time={Uri.EscapeDataString(at.UtcDateTime.ToString("o", CultureInfo.InvariantCulture))}";

        using var response = await _http.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Prometheus query failed ({StatusCode}) at {BaseAddress} for {Query}",
                (int)response.StatusCode,
                _http.BaseAddress,
                query);
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<PromQueryResponse>(JsonOptions, cancellationToken);
        if (payload?.Status != "success" || payload.Data?.Result is not { Count: > 0 } results)
            return [];

        var samples = new List<PromSample>(results.Count);
        foreach (var result in results)
        {
            var parsed = ParseSampleValue(result.Value);
            if (parsed is null)
                continue;
            var job = ReadJob(result.Metric);
            samples.Add(new PromSample(job, parsed.Value, result.Metric));
        }

        return samples;
    }

    private static string ReadJob(Dictionary<string, string>? metric)
    {
        if (metric is null)
            return "";
        if (metric.TryGetValue("exported_job", out var exported) && !string.IsNullOrWhiteSpace(exported))
            return exported;
        if (metric.TryGetValue("job", out var job) && !string.IsNullOrWhiteSpace(job))
            return job;
        return "";
    }

    private static double? ParseSampleValue(List<JsonElement>? value)
    {
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
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("data")]
        public PromQueryData? Data { get; set; }
    }

    private sealed class PromQueryData
    {
        [JsonPropertyName("result")]
        public List<PromQueryResult>? Result { get; set; }
    }

    private sealed class PromQueryResult
    {
        [JsonPropertyName("metric")]
        public Dictionary<string, string>? Metric { get; set; }

        [JsonPropertyName("value")]
        public List<JsonElement>? Value { get; set; }
    }
}
