using System.Collections.Concurrent;
using Confluent.Kafka;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Shared.Application.Models.Configs;
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
    private readonly KafkaConnection _kafkaConnection;

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
    /// Counts captured -Error and -Retry messages that belong to the supplied facility
    /// (JSON ResourceKey / ReadyForValidation.Key, X-Exception-Facility-Id, or a raw
    /// facility-id key). Messages with no identifiable facility still count so they
    /// cannot be ruled out as foreign.
    /// </summary>
    public int GetErrorCountForFacility(string? facilityId) =>
        CountForFacility(facilityId, e => true);

    public int GetDeadLetterCountForFacility(string? facilityId) =>
        CountForFacility(facilityId, e => e.IsDeadLetter);

    public int GetRetryCountForFacility(string? facilityId) =>
        CountForFacility(facilityId, e => e.IsRetry);

    /// <summary>
    /// Captured-error rows scoped to the supplied facility id (same filter rules as
    /// <see cref="GetErrorCountForFacility"/>). Useful for focused diagnostic dumps.
    /// </summary>
    public IReadOnlyList<string> GetErrorsForFacility(string? facilityId)
    {
        return [.. MatchingFacility(_capturedErrors, facilityId).Select(e => e.Message)];
    }

    private int CountForFacility(string? facilityId, Func<CapturedKafkaError, bool> predicate) =>
        MatchingFacility(_capturedErrors, facilityId).Count(predicate);

    private static IEnumerable<CapturedKafkaError> MatchingFacility(
        IEnumerable<CapturedKafkaError> errors,
        string? facilityId)
    {
        if (string.IsNullOrEmpty(facilityId))
            return errors;

        return errors.Where(e => KafkaDeadLetterContract.MatchesFacility(e.Key, e.Headers, facilityId));
    }

    private sealed record CapturedKafkaError(
        string? Key,
        IReadOnlyDictionary<string, string> Headers,
        bool IsDeadLetter,
        bool IsRetry,
        string Message);

    public KafkaErrorMonitor(IAutomationOutput output, AutomationConfig config, KafkaConnection kafkaConnection)
    {
        _output = output;
        _kafkaConnection = kafkaConnection;
        _kafkaBootstrapServers = string.Join(", ", kafkaConnection.BootstrapServers);
        _kafkaRestProxyBase = config.Kafka.RestProxyBaseUrl;
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

            if (_kafkaConnection.SaslProtocolEnabled)
            {
                config.SecurityProtocol = _kafkaConnection.Protocol;
                config.SaslMechanism = _kafkaConnection.Mechanism;
                config.SaslUsername = _kafkaConnection.SaslUsername;
                config.SaslPassword = _kafkaConnection.SaslPassword;
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

                    ObserveMessage(result.Topic, result.Message.Key, result.Message.Headers, result.Message.Value ?? "(null)");
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

    private void ObserveMessage(string topic, string? key, Headers? headers, string value)
    {
        var headerMap = KafkaDeadLetterContract.ReadHeaders(headers);
        var isResourcesNormalized = KafkaDeadLetterContract.IsResourcesNormalizedFailureTopic(topic);
        var isDeadLetter = KafkaDeadLetterContract.IsErrorTopic(topic);
        var isRetry = KafkaDeadLetterContract.IsRetryTopic(topic);

        var headerPreview = FormatHeaders(headerMap, isResourcesNormalized);
        var valuePreviewLength = KafkaDeadLetterContract.ValuePreviewLength(isResourcesNormalized);
        var resourceSummary = isResourcesNormalized
            ? KafkaDeadLetterContract.TrySummarizeResourcesNormalized(key, value)
            : null;

        var displayKey = key ?? "(null)";
        var message = $"[DIAG][Kafka][{topic}] Key={displayKey}{headerPreview} Value={Truncate(value, valuePreviewLength)}";
        if (!string.IsNullOrWhiteSpace(resourceSummary))
            message += $" | Parsed={resourceSummary}";

        _output.WriteLine(message);

        var captured = new CapturedKafkaError(
            Key: key,
            Headers: headerMap,
            IsDeadLetter: isDeadLetter,
            IsRetry: isRetry,
            Message: message);
        _capturedErrors.Add(captured);
    }

    private static string FormatHeaders(IReadOnlyDictionary<string, string> headers, bool isResourcesNormalizedFailureTopic)
    {
        if (headers.Count == 0)
            return "";

        var parts = headers.Select(header =>
        {
            var limit = KafkaDeadLetterContract.HeaderPreviewLength(header.Key, isResourcesNormalizedFailureTopic);
            return $"{header.Key}={Truncate(header.Value, limit)}";
        });

        return $" Headers=[{string.Join(", ", parts)}]";
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
