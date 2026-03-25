using System.Collections.Concurrent;
using System.Text;
using Confluent.Kafka;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests.Helpers;

/// <summary>
/// Monitors Kafka error and retry topics for dead-letter messages that indicate
/// processing failures in the pipeline. Uses a background consumer loop identical
/// to the service listeners (e.g., ReportScheduledListener).
/// </summary>
public class KafkaErrorMonitor : IAsyncDisposable
{
    private static readonly string KafkaBootstrapServers =
        Environment.GetEnvironmentVariable("E2E_KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9094";

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

    private static readonly string[] ErrorTopics =
    [
        "GenerateReportRequested-Error",
        "DataAcquisitionRequested-Error",
        "EvaluationRequested-Error",
        "MeasureReportGenerated-Error",
        "ResourceNormalized-Error",
        "ResourceAcquired-Error",
        "ReadyToAcquire-Error",
        "PatientEvent-Error",
        "ReadyForValidation-Error",
        "ValidationComplete-Error",
        "SubmitPayload-Error",
        "PayloadSubmitted-Error",
        "ReportScheduled-Error",
        "GenerateReportRequested-Retry",
        "DataAcquisitionRequested-Retry",
        "EvaluationRequested-Retry",
        "ReadyToAcquire-Retry",
    ];

    private readonly ConcurrentBag<string> _capturedErrors = [];

    public IReadOnlyList<string> CapturedErrors => [.. _capturedErrors];
    public bool HasErrors => !_capturedErrors.IsEmpty;

    public KafkaErrorMonitor(ITestOutputHelper output)
    {
        _output = output;
    }

    public Task InitializeAsync()
    {
        if (_initialized) return Task.CompletedTask;

        try
        {
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

            _consumer.Subscribe(ErrorTopics);
            _initialized = true;

            _cts = new CancellationTokenSource();
            _listenerTask = Task.Run(() => StartConsumerLoop(_cts.Token));

            _output.WriteLine($"[DIAG][Kafka] Listening on {ErrorTopics.Length} error/retry topics");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[DIAG][Kafka] Init failed: {ex.Message}");
        }

        return Task.CompletedTask;
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
