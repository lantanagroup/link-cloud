using System.Net;
using Newtonsoft.Json.Linq;
using RestSharp;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests.Helpers;

/// <summary>
/// Monitors Kafka error and retry topics via the Kafka REST Proxy for dead-letter
/// messages that indicate processing failures in the pipeline.
/// </summary>
public class KafkaErrorMonitor : IAsyncDisposable
{
    private static readonly string KafkaRestProxyBase =
        Environment.GetEnvironmentVariable("E2E_KAFKA_REST_PROXY_URL") ?? "http://localhost:8082";

    private static readonly string KafkaUser =
        Environment.GetEnvironmentVariable("E2E_KAFKA_USER") ?? "user";

    private static readonly string KafkaPassword =
        Environment.GetEnvironmentVariable("E2E_KAFKA_PASSWORD") ?? "password";

    private readonly ITestOutputHelper _output;
    private readonly RestClient _client;
    private readonly string _consumerGroup;
    private readonly string _instanceId;
    private string? _consumerBaseUri;
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

    private readonly List<string> _capturedErrors = [];

    public IReadOnlyList<string> CapturedErrors => _capturedErrors;
    public bool HasErrors => _capturedErrors.Count > 0;

    public KafkaErrorMonitor(ITestOutputHelper output)
    {
        _output = output;
        _client = new RestClient(KafkaRestProxyBase);
        _consumerGroup = $"e2e-diag-{Guid.NewGuid():N}";
        _instanceId = "diag-instance";
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            // Create consumer instance
            var createRequest = new RestRequest($"/consumers/{_consumerGroup}", Method.Post);
            createRequest.AddHeader("Content-Type", "application/vnd.kafka.v2+json");
            var createBody = new JObject
            {
                ["name"] = _instanceId,
                ["format"] = "binary",
                ["auto.offset.reset"] = "latest",
                ["auto.commit.enable"] = "true"
            };
            createRequest.AddStringBody(createBody.ToString(), DataFormat.Json);

            var createResponse = await _client.ExecuteAsync(createRequest);
            if (createResponse.StatusCode != HttpStatusCode.OK && createResponse.StatusCode != HttpStatusCode.Conflict)
            {
                _output.WriteLine($"[DIAG][Kafka] Failed to create consumer: {createResponse.StatusCode} {createResponse.Content}");
                return;
            }

            if (createResponse.Content != null)
            {
                var createJson = JObject.Parse(createResponse.Content);
                _consumerBaseUri = createJson["base_uri"]?.ToString();
            }

            if (string.IsNullOrEmpty(_consumerBaseUri))
            {
                _consumerBaseUri = $"{KafkaRestProxyBase}/consumers/{_consumerGroup}/instances/{_instanceId}";
            }

            // Subscribe to error topics
            var subscribeRequest = new RestRequest($"{_consumerBaseUri}/subscription", Method.Post);
            subscribeRequest.AddHeader("Content-Type", "application/vnd.kafka.v2+json");
            var subscribeBody = new JObject
            {
                ["topics"] = new JArray(ErrorTopics)
            };
            subscribeRequest.AddStringBody(subscribeBody.ToString(), DataFormat.Json);

            var subscribeResponse = await _client.ExecuteAsync(subscribeRequest);
            if (subscribeResponse.StatusCode != HttpStatusCode.NoContent &&
                subscribeResponse.StatusCode != HttpStatusCode.OK)
            {
                _output.WriteLine($"[DIAG][Kafka] Failed to subscribe: {subscribeResponse.StatusCode} {subscribeResponse.Content}");
                return;
            }

            _initialized = true;
            _output.WriteLine($"[DIAG][Kafka] Monitoring {ErrorTopics.Length} error/retry topics");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[DIAG][Kafka] Init failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Polls for new messages on the subscribed error topics and logs any findings.
    /// </summary>
    public async Task PollAsync()
    {
        if (!_initialized || _consumerBaseUri == null) return;

        try
        {
            var pollRequest = new RestRequest($"{_consumerBaseUri}/records", Method.Get);
            pollRequest.AddHeader("Accept", "application/vnd.kafka.binary.v2+json");

            var response = await _client.ExecuteAsync(pollRequest);
            if (response.StatusCode != HttpStatusCode.OK || string.IsNullOrEmpty(response.Content))
                return;

            var records = JArray.Parse(response.Content);
            foreach (var record in records)
            {
                var topic = record["topic"]?.ToString() ?? "unknown";
                var keyBase64 = record["key"]?.ToString();
                var valueBase64 = record["value"]?.ToString();

                var key = DecodeBase64(keyBase64);
                var value = DecodeBase64(valueBase64);

                var message = $"[DIAG][Kafka][{topic}] Key={key} Value={Truncate(value, 500)}";
                _output.WriteLine(message);
                _capturedErrors.Add(message);
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[DIAG][Kafka] Poll error: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed || !_initialized || _consumerBaseUri == null) return;
        _disposed = true;

        try
        {
            var deleteRequest = new RestRequest(_consumerBaseUri, Method.Delete);
            deleteRequest.AddHeader("Content-Type", "application/vnd.kafka.v2+json");
            await _client.ExecuteAsync(deleteRequest);
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    private static string DecodeBase64(string? base64)
    {
        if (string.IsNullOrEmpty(base64)) return "(null)";
        try
        {
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        }
        catch
        {
            return base64;
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
