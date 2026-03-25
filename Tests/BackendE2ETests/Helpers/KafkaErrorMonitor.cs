using System.Collections.Concurrent;
using System.Text;
using Confluent.Kafka;
using Newtonsoft.Json.Linq;
using RestSharp;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests.Helpers;

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
    private static readonly string KafkaBootstrapServers =
        Environment.GetEnvironmentVariable("E2E_KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9094";

    private static readonly string KafkaRestProxyBase =
        Environment.GetEnvironmentVariable("E2E_KAFKA_REST_PROXY_URL") ?? "http://localhost:8082";

    private static readonly string KafkaUser =
        Environment.GetEnvironmentVariable("E2E_KAFKA_USER") ?? "user";

    private static readonly string KafkaPassword =
        Environment.GetEnvironmentVariable("E2E_KAFKA_PASSWORD") ?? "password";

    private readonly ITestOutputHelper _output;
    private IConsumer<string, string>? _consumer;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private bool _initialized;
    private bool _disposed;

    private readonly ConcurrentBag<string> _capturedErrors = [];

    public IReadOnlyList<string> CapturedErrors => [.. _capturedErrors];
    public bool HasErrors => !_capturedErrors.IsEmpty;

    public KafkaErrorMonitor(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Queries the Kafka REST Proxy v3 API to discover all topics ending in -Error or -Retry.
    /// </summary>
    private async Task<string[]> DiscoverErrorAndRetryTopicsAsync()
    {
        var client = new RestClient(KafkaRestProxyBase);

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
                BootstrapServers = KafkaBootstrapServers,
                GroupId = $"e2e-diag-{Guid.NewGuid():N}",
                AutoOffsetReset = AutoOffsetReset.Latest,
                EnableAutoCommit = true,
                SecurityProtocol = SecurityProtocol.SaslPlaintext,
                SaslMechanism = SaslMechanism.Plain,
                SaslUsername = KafkaUser,
                SaslPassword = KafkaPassword,
                SessionTimeoutMs = 10000,
                SocketTimeoutMs = 5000,
            };

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
                    var headers = ExtractHeaders(result.Message.Headers);

                    var message = $"[DIAG][Kafka][{topic}] Key={key}{headers} Value={Truncate(value, 500)}";
                    _output.WriteLine(message);
                    _capturedErrors.Add(message);
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

    private static string ExtractHeaders(Headers? headers)
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
                parts.Add($"{header.Key}={Truncate(value, 100)}");
            }
            catch
            {
                // Skip malformed headers
            }
        }

        return parts.Count > 0 ? $" Headers=[{string.Join(", ", parts)}]" : "";
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
