using System.Collections.Concurrent;
using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Automation.Link.Configuration;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace LantanaGroup.Link.Automation.Link.Helpers;

/// <summary>
/// Monitors Kafka error and retry topics for dead-letter messages that indicate
/// processing failures in the pipeline. Uses a background consumer loop identical
/// to the service listeners (e.g., ReportScheduledListener).
///
/// Topics are discovered directly from the broker — any topic ending in -Error or
/// -Retry is automatically monitored.
/// </summary>
public class KafkaErrorMonitor : IAsyncDisposable
{
    private readonly string _kafkaBootstrapServers;
    private readonly string _kafkaRestProxyBase;
    private readonly string _kafkaUser;
    private readonly string _kafkaPassword;

    private readonly IAutomationOutput _output;
    private IConsumer<string, string>? _consumer;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private bool _initialized;
    private bool _disposed;

    private readonly ConcurrentBag<CapturedKafkaError> _capturedErrors = [];

    /// <summary>
    /// Formatted log lines for every error/retry message observed since startup. Includes
    /// messages keyed to OTHER facilities (e.g. retry traffic from a previous test); use
    /// <see cref="GetErrorCountForFacility"/> when you need a count scoped to a specific
    /// run.
    /// </summary>
    public IReadOnlyList<string> CapturedErrors => [.. _capturedErrors.Select(e => e.Message)];
    public bool HasErrors => !_capturedErrors.IsEmpty;

    /// <summary>
    /// Counts captured errors whose Kafka message key matches the supplied facility id
    /// (or has no key at all, so the error can't be ruled out as foreign). Errors keyed
    /// to a different facility are noise from a sibling test and are excluded.
    /// </summary>
    public int GetErrorCountForFacility(string? facilityId)
    {
        if (string.IsNullOrEmpty(facilityId))
            return _capturedErrors.Count;

        return _capturedErrors.Count(e =>
            string.IsNullOrEmpty(e.Key) ||
            string.Equals(e.Key, facilityId, StringComparison.Ordinal));
    }

    /// <summary>
    /// Captured-error rows scoped to the supplied facility id (same filter rules as
    /// <see cref="GetErrorCountForFacility"/>). Useful for focused diagnostic dumps.
    /// </summary>
    public IReadOnlyList<string> GetErrorsForFacility(string? facilityId)
    {
        if (string.IsNullOrEmpty(facilityId))
            return [.. _capturedErrors.Select(e => e.Message)];

        return [.. _capturedErrors
            .Where(e => string.IsNullOrEmpty(e.Key) || string.Equals(e.Key, facilityId, StringComparison.Ordinal))
            .Select(e => e.Message)];
    }

    private sealed record CapturedKafkaError(string? Key, string Message);

    private const int DefaultValuePreviewLength = 500;
    private const int DefaultHeaderPreviewLength = 100;
    private const int ResourceNormalizedValuePreviewLength = 4000;
    private const int ResourceNormalizedHeaderPreviewLength = 2000;

    public KafkaErrorMonitor(IAutomationOutput output, AutomationConfig config)
    {
        _output = output;
        _kafkaBootstrapServers = config.Kafka.BootstrapServers;
        _kafkaRestProxyBase = config.Kafka.RestProxyBaseUrl;
        _kafkaUser = config.Kafka.User;
        _kafkaPassword = config.Kafka.Password;
    }

    /// <summary>
    /// Queries the Kafka REST Proxy v3 API to discover all topics ending in -Error or -Retry.
    /// </summary>
    private async Task<string[]> DiscoverErrorAndRetryTopicsAsync()
    {
        var client = new RestClient(_kafkaRestProxyBase);

        var clusterResponse = await client.ExecuteAsync(new RestRequest("/v3/clusters", Method.Get));
        if (clusterResponse.StatusCode != System.Net.HttpStatusCode.OK || clusterResponse.Content == null)
            throw new InvalidOperationException($"Failed to query Kafka clusters: {clusterResponse.StatusCode}");

        var clusterId = JObject.Parse(clusterResponse.Content)["data"]?[0]?["cluster_id"]?.ToString()
            ?? throw new InvalidOperationException("No cluster found in REST proxy response");

        var topicsResponse = await client.ExecuteAsync(new RestRequest($"/v3/clusters/{clusterId}/topics", Method.Get));
        if (topicsResponse.StatusCode != System.Net.HttpStatusCode.OK || topicsResponse.Content == null)
            throw new InvalidOperationException($"Failed to query Kafka topics: {topicsResponse.StatusCode}");

        return JObject.Parse(topicsResponse.Content)["data"]!
            .Select(t => t["topic_name"]?.ToString())
            .Where(t => t != null &&
                        (t.EndsWith("-Error", StringComparison.OrdinalIgnoreCase) ||
                         t.EndsWith("-Retry", StringComparison.OrdinalIgnoreCase)))
            .Order()
            .ToArray()!;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            var topics = await DiscoverErrorAndRetryTopicsAsync();

            if (topics.Length == 0)
            {
                _output.WriteLine("[DIAG][Kafka] No -Error or -Retry topics found on broker");
                return;
            }

            var config = new ConsumerConfig
            {
                BootstrapServers = _kafkaBootstrapServers,
                GroupId = $"e2e-diag-{Guid.NewGuid():N}",
                AutoOffsetReset = AutoOffsetReset.Latest,
                EnableAutoCommit = true,
                SessionTimeoutMs = 10000,
                SocketTimeoutMs = 5000,
            };

            // Only enable SASL when credentials are actually configured. The Link services
            // gate SASL behind KafkaConnection.SaslProtocolEnabled (default false) and the
            // local/dev/docker brokers use PLAINTEXT. Forcing SaslPlaintext/Plain here with
            // empty credentials makes librdkafka throw "sasl.username and sasl.password must
            // be set", which silently disabled dead-letter monitoring for the whole run.
            if (!string.IsNullOrWhiteSpace(_kafkaUser) && !string.IsNullOrWhiteSpace(_kafkaPassword))
            {
                config.SecurityProtocol = SecurityProtocol.SaslPlaintext;
                config.SaslMechanism = SaslMechanism.Plain;
                config.SaslUsername = _kafkaUser;
                config.SaslPassword = _kafkaPassword;
            }

            _consumer = new ConsumerBuilder<string, string>(config)
                .SetErrorHandler((_, e) =>
                {
                    if (e.IsFatal)
                        _output.WriteLine($"[DIAG][Kafka] Fatal consumer error: {e.Reason}");
                })
                .Build();

            _consumer.Subscribe(topics);
            _initialized = true;

            _cts = new CancellationTokenSource();
            _listenerTask = Task.Run(() => StartConsumerLoop(_cts.Token));

            _output.WriteLine($"[DIAG][Kafka] Listening on {topics.Length} error/retry topics discovered from broker");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[DIAG][Kafka] Init failed: {ex.Message}");
        }
    }

    private void StartConsumerLoop(CancellationToken cancellationToken)
    {
        if (_consumer == null) return;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(cancellationToken);
                    if (result?.Message == null) continue;

                    var topic = result.Topic;
                    var key = result.Message.Key ?? "(null)";
                    var value = result.Message.Value ?? "(null)";

                    var isResourceNormalizedError =
                        topic.Equals("ResourceNormalized-Error", StringComparison.OrdinalIgnoreCase);

                    var headerPreviewLength = isResourceNormalizedError
                        ? ResourceNormalizedHeaderPreviewLength
                        : DefaultHeaderPreviewLength;

                    var valuePreviewLength = isResourceNormalizedError
                        ? ResourceNormalizedValuePreviewLength
                        : DefaultValuePreviewLength;

                    var headers = ExtractHeaders(result.Message.Headers, headerPreviewLength);
                    var resourceSummary = isResourceNormalizedError
                        ? TryBuildResourceNormalizedSummary(value)
                        : null;

                    var message = $"[DIAG][Kafka][{topic}] Key={key}{headers} Value={Truncate(value, valuePreviewLength)}";
                    if (!string.IsNullOrWhiteSpace(resourceSummary))
                        message += $" | Parsed={resourceSummary}";

                    _output.WriteLine(message);
                    _capturedErrors.Add(new CapturedKafkaError(
                        Key: result.Message.Key,
                        Message: message));
                }
                catch (ConsumeException ex)
                {
                    _output.WriteLine($"[DIAG][Kafka] Consume error: {ex.Error.Reason}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[DIAG][Kafka] Listener error: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (_cts != null)
            {
                await _cts.CancelAsync();

                if (_listenerTask != null)
                {
                    try { await _listenerTask; }
                    catch (OperationCanceledException) { }
                }

                _cts.Dispose();
            }

            _consumer?.Close();
            _consumer?.Dispose();
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    private static string ExtractHeaders(Headers? headers, int maxLengthPerHeaderValue)
    {
        if (headers == null || headers.Count == 0) return "";

        var parts = new List<string>();
        foreach (var header in headers)
        {
            try
            {
                var value = header.GetValueBytes() != null
                    ? Encoding.UTF8.GetString(header.GetValueBytes())
                    : "(null)";

                var limit = ShouldUseExtendedHeaderLimit(header.Key)
                    ? 12000
                    : maxLengthPerHeaderValue;

                parts.Add($"{header.Key}={Truncate(value, limit)}");
            }
            catch
            {
                // Skip malformed headers
            }
        }

        return parts.Count > 0 ? $" Headers=[{string.Join(", ", parts)}]" : "";
    }

    private static bool ShouldUseExtendedHeaderLimit(string headerKey)
    {
        return headerKey.Contains("exception-stacktrace", StringComparison.OrdinalIgnoreCase)
               || headerKey.Contains("exception-message", StringComparison.OrdinalIgnoreCase)
               || headerKey.Contains("exception-cause", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryBuildResourceNormalizedSummary(string rawValue)
    {
        try
        {
            var json = JObject.Parse(rawValue);
            var patientId = json["PatientId"]?.ToString() ?? json["patientId"]?.ToString();
            var queryType = json["QueryType"]?.ToString() ?? json["queryType"]?.ToString();
            var reportableEvent = json["ReportableEvent"]?.ToString() ?? json["reportableEvent"]?.ToString();

            var resourceToken = json["Resource"] ?? json["resource"];
            var resourceType = resourceToken?["resourceType"]?.ToString();
            var resourceId = resourceToken?["id"]?.ToString();

            var scheduledReports = json["ScheduledReports"] ?? json["scheduledReports"];
            var scheduledCount = scheduledReports is JArray reports ? reports.Count : 0;

            return $"patientId={patientId ?? "(null)"}, queryType={queryType ?? "(null)"}, reportableEvent={reportableEvent ?? "(null)"}, resourceType={resourceType ?? "(null)"}, resourceId={resourceId ?? "(null)"}, scheduledReports={scheduledCount}";
        }
        catch
        {
            return null;
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
