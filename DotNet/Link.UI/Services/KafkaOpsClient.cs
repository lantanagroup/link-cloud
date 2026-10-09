using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace Link.UI.Services;

public sealed class KafkaOpsClient : IKafkaTopicHoldSource
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContext;
    private readonly KafkaOpsFixture _fixture;

    public KafkaOpsClient(HttpClient http, IHttpContextAccessor httpContext, KafkaOpsFixture fixture)
    {
        _http = http;
        _httpContext = httpContext;
        _fixture = fixture;
    }

    public Task<KafkaOpsCall<KafkaTopicsResponse>> GetTopicsAsync(CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.Topics());
        return SendAsync<KafkaTopicsResponse>(HttpMethod.Get, "api/ops/kafka/topics", null, cancellationToken);
    }

    public Task<KafkaOpsCall<KafkaGroupsResponse>> GetGroupsAsync(bool includeTestGroups, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.Groups(includeTestGroups));
        return SendAsync<KafkaGroupsResponse>(HttpMethod.Get, "api/ops/kafka/groups?includeTestGroups=" + (includeTestGroups ? "true" : "false"), null, cancellationToken);
    }

    public Task<KafkaOpsCall<KafkaCapabilitiesResponse>> GetCapabilitiesAsync(CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.Capabilities());
        return SendAsync<KafkaCapabilitiesResponse>(HttpMethod.Get, "api/ops/kafka/capabilities", null, cancellationToken);
    }

    public Task<KafkaOpsCall<ClusterSnapshot>> GetClusterAsync(CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.Cluster());
        return SendAsync<ClusterSnapshot>(HttpMethod.Get, "api/ops/kafka/cluster", null, cancellationToken);
    }

    public Task<KafkaOpsCall<InfraStatus>> GetInfraAsync(CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.Infra());
        return SendAsync<InfraStatus>(HttpMethod.Get, "api/ops/kafka/infra", null, cancellationToken);
    }

    public Task<KafkaOpsCall<PartitionPlan>> PlanAsync(string topic, int partitions, bool overrideQuietWindow, string? overrideReason, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.PlanPartitions(topic, partitions, overrideQuietWindow, overrideReason));
        return SendAsync<PartitionPlan>(HttpMethod.Post, "api/ops/kafka/topics/" + Uri.EscapeDataString(topic) + "/partitions/plan", new
        {
            partitions,
            overrideQuietWindow,
            overrideReason
        }, cancellationToken, keepBodyOnFailure: true);
    }

    public Task<KafkaOpsCall<PartitionPlan>> PlanFamilyAsync(string topic, bool overrideQuietWindow, string? overrideReason, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.PlanFamily(topic, overrideQuietWindow, overrideReason));
        return SendAsync<PartitionPlan>(HttpMethod.Post, "api/ops/kafka/topics/" + Uri.EscapeDataString(topic) + "/family/plan", new
        {
            overrideQuietWindow,
            overrideReason
        }, cancellationToken, keepBodyOnFailure: true);
    }

    public Task<KafkaOpsCall<ChangeRequestRecord>> CreateFamilyAsync(string topic, string reason, bool overrideQuietWindow, string? overrideReason, string? confirmation, string correlationId, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.CreateFamily(topic, reason, overrideQuietWindow, overrideReason, confirmation, correlationId));
        return SendAsync<ChangeRequestRecord>(HttpMethod.Post, "api/ops/kafka/topics/" + Uri.EscapeDataString(topic) + "/family", new
        {
            topic,
            reason,
            overrideQuietWindow,
            overrideReason,
            confirmation
        }, cancellationToken, correlationId);
    }

    public Task<KafkaOpsCall<ChangeRequestRecord>> CreateAsync(string topic, int partitions, string reason, bool overrideQuietWindow, string? overrideReason, string? confirmation, string correlationId, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.CreatePartitions(topic, partitions, reason, overrideQuietWindow, overrideReason, confirmation, correlationId));
        return SendAsync<ChangeRequestRecord>(HttpMethod.Post, "api/ops/kafka/change-requests", new
        {
            topic,
            partitions,
            reason,
            overrideQuietWindow,
            overrideReason,
            confirmation
        }, cancellationToken, correlationId);
    }

    public Task<KafkaOpsCall<ReplicaScalePlan>> PlanScaleAsync(string groupId, int replicas, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.PlanScale(groupId, replicas));
        return SendAsync<ReplicaScalePlan>(HttpMethod.Post, "api/ops/kafka/groups/" + Uri.EscapeDataString(groupId) + "/replicas/plan", new { replicas }, cancellationToken, keepBodyOnFailure: true);
    }

    public Task<KafkaOpsCall<ChangeRequestRecord>> CreateScaleAsync(string groupId, int replicas, string reason, string correlationId, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.CreateScale(groupId, replicas, reason, correlationId));
        return SendAsync<ChangeRequestRecord>(HttpMethod.Post, "api/ops/kafka/groups/" + Uri.EscapeDataString(groupId) + "/replicas", new { replicas, reason }, cancellationToken, correlationId);
    }

    public Task<KafkaOpsCall<BrokerMovePlan>> PlanDecommissionAsync(int brokerId, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.PlanDecommission(brokerId));
        return SendAsync<BrokerMovePlan>(HttpMethod.Post, "api/ops/kafka/brokers/" + brokerId + "/decommission/plan", null, cancellationToken, keepBodyOnFailure: true);
    }

    public Task<KafkaOpsCall<ChangeRequestRecord>> CreateDecommissionAsync(int brokerId, string reason, string correlationId, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.CreateBroker("DecommissionBroker", brokerId, reason, correlationId));
        return SendAsync<ChangeRequestRecord>(HttpMethod.Post, "api/ops/kafka/brokers/" + brokerId + "/decommission", new { reason }, cancellationToken, correlationId);
    }

    public Task<KafkaOpsCall<BrokerMovePlan>> PlanRebalanceAsync(int brokerId, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.PlanRebalance(brokerId));
        return SendAsync<BrokerMovePlan>(HttpMethod.Post, "api/ops/kafka/brokers/" + brokerId + "/rebalance/plan", null, cancellationToken, keepBodyOnFailure: true);
    }

    public Task<KafkaOpsCall<ChangeRequestRecord>> CreateRebalanceAsync(int brokerId, string reason, string correlationId, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.CreateBroker("Rebalance", brokerId, reason, correlationId));
        return SendAsync<ChangeRequestRecord>(HttpMethod.Post, "api/ops/kafka/brokers/" + brokerId + "/rebalance", new { reason }, cancellationToken, correlationId);
    }

    public Task<KafkaOpsCall<ChangeRequestRecord>> CreateAddBrokerAsync(string reason, string correlationId, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.CreateBroker("AddBroker", -1, reason, correlationId));
        return SendAsync<ChangeRequestRecord>(HttpMethod.Post, "api/ops/kafka/brokers", new { reason }, cancellationToken, correlationId);
    }

    public Task<KafkaOpsCall<ChangeRequestRecord>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.Get(id));
        return SendAsync<ChangeRequestRecord>(HttpMethod.Get, "api/ops/kafka/change-requests/" + id.ToString("D"), null, cancellationToken);
    }

    public Task<KafkaOpsCall<ChangeRequestRecord>> ApproveAsync(Guid id, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.Approve(id));
        return SendAsync<ChangeRequestRecord>(HttpMethod.Post, "api/ops/kafka/change-requests/" + id.ToString("D") + "/approve", new { }, cancellationToken);
    }

    public Task<KafkaOpsCall<ChangeRequestRecord>> RejectAsync(Guid id, string reason, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.Reject(id, reason));
        return SendAsync<ChangeRequestRecord>(HttpMethod.Post, "api/ops/kafka/change-requests/" + id.ToString("D") + "/reject", new { reason }, cancellationToken);
    }

    public Task<KafkaOpsCall<ChangeRequestRecord>> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.Execute(id));
        return SendAsync<ChangeRequestRecord>(HttpMethod.Post, "api/ops/kafka/change-requests/" + id.ToString("D") + "/execute", new { }, cancellationToken);
    }

    public Task<KafkaOpsCall<ChangeRequestRecord>> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.Cancel(id));
        return SendAsync<ChangeRequestRecord>(HttpMethod.Post, "api/ops/kafka/change-requests/" + id.ToString("D") + "/cancel", new { }, cancellationToken);
    }

    public Task<KafkaOpsCall<List<string>>> GetHoldsAsync(CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(new KafkaOpsCall<List<string>> { Status = 200, Value = [] });
        return SendAsync<List<string>>(HttpMethod.Get, "api/ops/kafka/holds", null, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetHeldTopicsAsync(CancellationToken cancellationToken)
    {
        var holds = await GetHoldsAsync(cancellationToken);
        return holds.Value ?? [];
    }

    public Task<KafkaOpsCall<KafkaTopicDetail>> GetDetailAsync(string topic, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.Detail(topic));
        return SendAsync<KafkaTopicDetail>(HttpMethod.Get, "api/ops/kafka/topics/" + Uri.EscapeDataString(topic) + "/detail", null, cancellationToken);
    }

    public Task<KafkaOpsCall<KafkaTopicConfigs>> GetConfigsAsync(string topic, string? diff, CancellationToken cancellationToken)
    {
        var query = string.IsNullOrWhiteSpace(diff) ? "" : "?diff=" + Uri.EscapeDataString(diff);
        if (_fixture.Active)
            return Task.FromResult(_fixture.Configs(topic, diff));
        return SendAsync<KafkaTopicConfigs>(HttpMethod.Get, "api/ops/kafka/topics/" + Uri.EscapeDataString(topic) + "/configs" + query, null, cancellationToken);
    }

    public Task<KafkaOpsCall<KafkaMigrationPlan>> PlanMigrationAsync(string topic, int partitions, bool backupSkip, bool backupSkipAcknowledged, IReadOnlyList<string> acknowledgedGroups, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(new KafkaOpsCall<KafkaMigrationPlan> { Status = 200, Value = new KafkaMigrationPlan { Accepted = false, Summary = "Fixture mode does not run a migration." } });
        return SendAsync<KafkaMigrationPlan>(HttpMethod.Post, "api/ops/kafka/topics/" + Uri.EscapeDataString(topic) + "/migrations/plan", new { partitions, backupSkip, backupSkipAcknowledged, acknowledgedGroups }, cancellationToken, keepBodyOnFailure: true);
    }

    public Task<KafkaOpsCall<KafkaMigrationRecord>> RequestMigrationAsync(string topic, int partitions, string reason, string confirmation, bool backupSkip, bool backupSkipAcknowledged, IReadOnlyList<string> acknowledgedGroups, string planHash, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(new KafkaOpsCall<KafkaMigrationRecord> { Status = 403, Error = "Fixture mode does not run a migration." });
        return SendAsync<KafkaMigrationRecord>(HttpMethod.Post, "api/ops/kafka/migrations", new { topic, partitions, reason, confirmation, backupSkip, backupSkipAcknowledged, acknowledgedGroups, planHash }, cancellationToken);
    }

    public Task<KafkaOpsCall<KafkaMigrationRecord>> GetMigrationAsync(Guid id, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(new KafkaOpsCall<KafkaMigrationRecord> { Status = 404, Error = "That migration was not found." });
        return SendAsync<KafkaMigrationRecord>(HttpMethod.Get, "api/ops/kafka/migrations/" + id.ToString("D"), null, cancellationToken);
    }

    public Task<KafkaOpsCall<KafkaMigrationRecord>> ApproveMigrationAsync(Guid id, CancellationToken cancellationToken) =>
        PostMigrationAsync(id, "approve", null, cancellationToken);

    public Task<KafkaOpsCall<KafkaMigrationRecord>> RejectMigrationAsync(Guid id, string reason, CancellationToken cancellationToken) =>
        PostMigrationAsync(id, "reject", new { reason }, cancellationToken);

    public Task<KafkaOpsCall<KafkaMigrationRecord>> ExecuteMigrationAsync(Guid id, CancellationToken cancellationToken) =>
        PostMigrationAsync(id, "execute", new { }, cancellationToken);

    public Task<KafkaOpsCall<KafkaMigrationRecord>> GoMigrationAsync(Guid id, string confirmation, CancellationToken cancellationToken) =>
        PostMigrationAsync(id, "go", new { confirmation }, cancellationToken);

    public Task<KafkaOpsCall<KafkaMigrationRecord>> AbortMigrationAsync(Guid id, CancellationToken cancellationToken) =>
        PostMigrationAsync(id, "abort", new { }, cancellationToken);

    public Task<KafkaOpsCall<KafkaMigrationRecord>> RecoverMigrationAsync(Guid id, string confirmation, string action, CancellationToken cancellationToken) =>
        PostMigrationAsync(id, "recover", new { confirmation, action }, cancellationToken);

    public Task<KafkaOpsCall<KafkaMigrationRecord>> ManualStepAsync(Guid id, string workload, CancellationToken cancellationToken) =>
        PostMigrationAsync(id, "manual-step", new { workload }, cancellationToken);

    public Task<KafkaOpsCall<string>> RunbookAsync(Guid id, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(new KafkaOpsCall<string> { Status = 200, Value = "The recreated topic starts empty. Consumer groups are committed at offset 0. The backup is kept." });
        return SendAsync<string>(HttpMethod.Get, "api/ops/kafka/migrations/" + id.ToString("D") + "/runbook", null, cancellationToken);
    }

    public Task<KafkaOpsCall<KafkaBrowsePage>> GetMessagesAsync(string topic, string mode, IReadOnlyList<int> partitions, long? offset, long? timestamp, int limit, string key, string headerName, string headerValue, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.Messages(topic, mode, partitions, offset, timestamp, limit, key, headerName, headerValue));
        return SendAsync<KafkaBrowsePage>(HttpMethod.Get, MessagesPath(topic, "messages", mode, partitions, offset, timestamp, limit, key, headerName, headerValue), null, cancellationToken);
    }

    public Task<KafkaOpsCall<KafkaFamilyView>> GetFamilyAsync(string topic, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(_fixture.Family(topic));
        return SendAsync<KafkaFamilyView>(HttpMethod.Get, "api/ops/kafka/topics/" + Uri.EscapeDataString(topic) + "/family", null, cancellationToken);
    }

    public Task<KafkaOpsCall<byte[]>> ExportMessagesAsync(string topic, string mode, IReadOnlyList<int> partitions, long? offset, long? timestamp, int limit, string key, string headerName, string headerValue, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
        {
            var page = _fixture.Messages(topic, mode, partitions, offset, timestamp, limit, key, headerName, headerValue);
            if (page.Value is null)
                return Task.FromResult(new KafkaOpsCall<byte[]> { Status = page.Status, Error = page.Error });
            var json = JsonSerializer.Serialize(page.Value.Records, ExportJson);
            return Task.FromResult(new KafkaOpsCall<byte[]> { Status = 200, Value = Encoding.UTF8.GetBytes(json) });
        }

        return SendBytesAsync(MessagesPath(topic, "messages/export", mode, partitions, offset, timestamp, limit, key, headerName, headerValue), cancellationToken);
    }

    public Task<KafkaOpsCall<string>> DeleteBackupAsync(string name, string confirmation, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(new KafkaOpsCall<string> { Status = 204, Value = "" });
        return SendAsync<string>(HttpMethod.Post, "api/ops/kafka/backups/" + Uri.EscapeDataString(name) + "/delete", new { confirmation }, cancellationToken);
    }

    private Task<KafkaOpsCall<KafkaMigrationRecord>> PostMigrationAsync(Guid id, string action, object? body, CancellationToken cancellationToken)
    {
        if (_fixture.Active)
            return Task.FromResult(new KafkaOpsCall<KafkaMigrationRecord> { Status = 404, Error = "That migration was not found." });
        return SendAsync<KafkaMigrationRecord>(HttpMethod.Post, "api/ops/kafka/migrations/" + id.ToString("D") + "/" + action, body ?? new { }, cancellationToken);
    }

    private static readonly JsonSerializerOptions ExportJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static string MessagesPath(string topic, string action, string mode, IReadOnlyList<int> partitions, long? offset, long? timestamp, int limit, string key, string headerName, string headerValue)
    {
        var pairs = new List<string>
        {
            "mode=" + Uri.EscapeDataString(mode ?? ""),
            "limit=" + limit.ToString()
        };
        foreach (var partition in partitions)
            pairs.Add("partition=" + partition.ToString());
        if (offset is long at)
            pairs.Add("offset=" + at.ToString());
        if (timestamp is long unixMs)
            pairs.Add("timestamp=" + unixMs.ToString());
        if (!string.IsNullOrEmpty(key))
            pairs.Add("key=" + Uri.EscapeDataString(key));
        if (!string.IsNullOrEmpty(headerName))
            pairs.Add("headerName=" + Uri.EscapeDataString(headerName));
        if (!string.IsNullOrEmpty(headerValue))
            pairs.Add("headerValue=" + Uri.EscapeDataString(headerValue));
        return "api/ops/kafka/topics/" + Uri.EscapeDataString(topic) + "/" + action + "?" + string.Join("&", pairs);
    }

    private async Task<KafkaOpsCall<byte[]>> SendBytesAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        var cookie = _httpContext.HttpContext?.Request.Headers.Cookie.ToString();
        if (!string.IsNullOrWhiteSpace(cookie))
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await _http.SendAsync(request, cancellationToken);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var text = Encoding.UTF8.GetString(bytes);
            return new KafkaOpsCall<byte[]>
            {
                Status = (int)response.StatusCode,
                Error = ProblemDetail(text) ?? "The operations service returned " + (int)response.StatusCode + "."
            };
        }

        return new KafkaOpsCall<byte[]> { Status = (int)response.StatusCode, Value = bytes };
    }

    private async Task<KafkaOpsCall<T>> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken, string? correlationId = null, bool keepBodyOnFailure = false)
    {
        using var request = new HttpRequestMessage(method, path);
        var cookie = _httpContext.HttpContext?.Request.Headers.Cookie.ToString();
        if (!string.IsNullOrWhiteSpace(cookie))
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(correlationId))
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        using var response = await _http.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            T? failed = default;
            if (keepBodyOnFailure && !string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    failed = JsonSerializer.Deserialize<T>(text, Json);
                }
                catch (JsonException)
                {
                    failed = default;
                }
            }

            return new KafkaOpsCall<T>
            {
                Status = (int)response.StatusCode,
                Value = failed,
                Error = ProblemDetail(text) ?? "The operations service returned " + (int)response.StatusCode + "."
            };
        }

        if (string.IsNullOrWhiteSpace(text))
            return new KafkaOpsCall<T> { Status = (int)response.StatusCode, Value = typeof(T) == typeof(string) ? (T)(object)"" : default };

        if (typeof(T) == typeof(string))
            return new KafkaOpsCall<T> { Status = (int)response.StatusCode, Value = (T)(object)text };

        var value = JsonSerializer.Deserialize<T>(text, Json);
        return new KafkaOpsCall<T> { Status = (int)response.StatusCode, Value = value };
    }

    private static string? ProblemDetail(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                return detail.GetString();
            if (document.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                var messages = errors.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(item => !string.IsNullOrWhiteSpace(item));
                var joined = string.Join(" ", messages);
                if (joined.Length > 0)
                    return joined;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}

public sealed class KafkaOpsCall<T>
{
    public int Status { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    public bool Ok => Error is null && Value is not null;
}

public sealed class KafkaTopicsResponse
{
    public List<KafkaTopicRow> Topics { get; set; } = [];
    public int Cap { get; set; }
    public bool ReadOnly { get; set; }
    public string? Error { get; set; }
    public string? GroupsError { get; set; }
}

public sealed class KafkaTopicRow
{
    public string Topic { get; set; } = "";
    public string Family { get; set; } = "";
    public string KeyClass { get; set; } = "";
    public string KeyShape { get; set; } = "";
    public bool HardBlocked { get; set; }
    public bool OrderSensitive { get; set; }
    public int Partitions { get; set; }
    public int RetryPartitions { get; set; }
    public int ErrorPartitions { get; set; }
    public bool RetryBehind { get; set; }
    public bool ErrorBehind { get; set; }
    public int ReplicationFactor { get; set; }
    public Dictionary<string, string> Configs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public double ProduceRatePerSecond { get; set; }
    public bool ProduceRateKnown { get; set; } = true;
    public long TotalLag { get; set; }
    public bool LagKnown { get; set; } = true;
    public int MaxReplicas { get; set; }
    public List<string> Groups { get; set; } = [];
    public string? Error { get; set; }
    public bool FullIsr { get; set; } = true;
    public bool LeadersSkewed { get; set; }
    public long EstimatedBytes { get; set; }
    public string SizeClass { get; set; } = "";
    public bool ServiceRetryBehind { get; set; }
    public bool ServiceRedriveBehind { get; set; }
    public bool PinnedSiblingBehind { get; set; }
    public int TopicsFilePartitions { get; set; } = 3;
    public bool PartitionDrift { get; set; }
    public bool Slice1Eligible { get; set; }
    public string MigrationEligibility { get; set; } = "";
    public string PartitionAddReason { get; set; } = "";
}

public sealed class KafkaGroupsResponse
{
    public List<KafkaGroupRow> Groups { get; set; } = [];
    public string? Error { get; set; }
}

public sealed class KafkaCapabilitiesResponse
{
    public List<KafkaTopicCapability> Topics { get; set; } = [];
    public string? Error { get; set; }
}

public sealed class KafkaTopicCapability
{
    public string Topic { get; set; } = "";
    public List<string> AuthorizedOperations { get; set; } = [];
    public bool? CanAlterPartitions { get; set; }
    public string? Error { get; set; }
}

public sealed class KafkaGroupRow
{
    public string GroupId { get; set; } = "";
    public string State { get; set; } = "";
    public List<KafkaMemberRow> Members { get; set; } = [];
    public List<KafkaPartitionLagRow> Partitions { get; set; } = [];
    public long TotalLag { get; set; }
    public List<string> UnownedPartitions { get; set; } = [];
    public int MembersOnExpectedConfig { get; set; }
    public bool Catalogued { get; set; } = true;
}

public sealed class KafkaMemberRow
{
    public string ClientId { get; set; } = "";
    public string Host { get; set; } = "";
    public List<string> Assignment { get; set; } = [];
    public bool AdvertisesExpectedConfig { get; set; }
}

public sealed class KafkaPartitionLagRow
{
    public string Topic { get; set; } = "";
    public int Partition { get; set; }
    public long HighWatermark { get; set; }
    public long Committed { get; set; }
    public long Lag { get; set; }
    public bool Owned { get; set; }
    public string GroupId { get; set; } = "";
}

public sealed class PartitionPlan
{
    public bool Accepted { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Notes { get; set; } = [];
    public string Topic { get; set; } = "";
    public string Family { get; set; } = "";
    public string RetryTopic { get; set; } = "";
    public string ErrorTopic { get; set; } = "";
    public string KeyClass { get; set; } = "";
    public string KeyShape { get; set; } = "";
    public bool HardBlocked { get; set; }
    public int CurrentPartitions { get; set; }
    public int RequestedPartitions { get; set; }
    public int MaxReplicas { get; set; }
    public bool SecondApproverRequired { get; set; }
    public bool QuietWindowRequired { get; set; }
    public bool QuietWindowMet { get; set; }
    public bool Irreversible { get; set; } = true;
    public bool FamilyCompletion { get; set; }
    public List<string> TopicsToRaise { get; set; } = [];
    public List<string> AffectedGroups { get; set; } = [];
    public string Summary { get; set; } = "";
}

public sealed class ChangeRequestRecord
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = "PartitionIncrease";
    public string GroupId { get; set; } = "";
    public int DesiredReplicas { get; set; }
    public int BeforeReplicas { get; set; }
    public int BrokerId { get; set; } = -1;
    public string Progress { get; set; } = "";
    public string Topic { get; set; } = "";
    public string Family { get; set; } = "";
    public string RetryTopic { get; set; } = "";
    public int BeforePartitions { get; set; }
    public int RequestedPartitions { get; set; }
    public int MaxReplicas { get; set; }
    public string KeyClass { get; set; } = "";
    public bool SecondApproverRequired { get; set; }
    public string Reason { get; set; } = "";
    public string OverrideReason { get; set; } = "";
    public string Requester { get; set; } = "";
    public string Approver { get; set; } = "";
    public string DryRunSummary { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? ApprovedUtc { get; set; }
    public DateTimeOffset? ExecutedUtc { get; set; }
    public DateTimeOffset? ConvergedUtc { get; set; }
    public DateTimeOffset? ClosedUtc { get; set; }
    public string Failure { get; set; } = "";
    public string Warning { get; set; } = "";
    public bool AlertRaised { get; set; }
    public List<GroupProgressRow> Groups { get; set; } = [];
}

public sealed class GroupProgressRow
{
    public string GroupId { get; set; } = "";
    public int AssignedPartitions { get; set; }
    public int ExpectedPartitions { get; set; }
    public bool Complete { get; set; }
}

public sealed class ClusterSnapshot
{
    public int BrokerCount { get; set; }
    public int? ControllerId { get; set; }
    public string ControllerUnavailableReason { get; set; } = "";
    public int UnderReplicatedPartitions { get; set; }
    public int OfflinePartitions { get; set; }
    public int IsrShrunkPartitions { get; set; }
    public bool LogDirsAvailable { get; set; }
    public string LogDirDetail { get; set; } = "";
    public List<BrokerSnapshot> Brokers { get; set; } = [];
    public List<PartitionPlacement> Placements { get; set; } = [];
    public string? Error { get; set; }
}

public sealed class BrokerSnapshot
{
    public int Id { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string Rack { get; set; } = "";
    public string State { get; set; } = "";
    public int PartitionCount { get; set; }
    public int LeaderCount { get; set; }
    public long LogDirBytes { get; set; } = -1;
}

public sealed class PartitionPlacement
{
    public string Topic { get; set; } = "";
    public int Partition { get; set; }
    public int Leader { get; set; }
    public List<int> Replicas { get; set; } = [];
    public List<int> Isr { get; set; } = [];
}

public sealed class InfraStatus
{
    public string Provider { get; set; } = "Disabled";
    public bool Enabled { get; set; }
    public string Detail { get; set; } = "";
}

public sealed class ReplicaScalePlan
{
    public bool Accepted { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Notes { get; set; } = [];
    public string GroupId { get; set; } = "";
    public int CurrentMembers { get; set; }
    public int DesiredMembers { get; set; }
    public int PartitionCount { get; set; }
    public int Ceiling { get; set; }
    public bool SecondApproverRequired { get; set; }
    public string Summary { get; set; } = "";
}

public sealed class BrokerMovePlan
{
    public bool Accepted { get; set; }
    public bool AlreadyEmpty { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Notes { get; set; } = [];
    public int BrokerId { get; set; }
    public List<ReplicaMove> Moves { get; set; } = [];
    public bool SecondApproverRequired { get; set; } = true;
    public string Summary { get; set; } = "";
}

public sealed class KafkaMigrationPlan
{
    public bool Accepted { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public string PlanHash { get; set; } = "";
    public int WindowMinutes { get; set; }
    public int AlertMinutes { get; set; }
    public List<string> StopSet { get; set; } = [];
    public List<string> Siblings { get; set; } = [];
    public string Summary { get; set; } = "";
}

public sealed class KafkaMigrationRecord
{
    public Guid Id { get; set; }
    public string Topic { get; set; } = "";
    public int OriginalPartitions { get; set; }
    public int TargetPartitions { get; set; }
    public int Step { get; set; }
    public string PlanHash { get; set; } = "";
    public string Requester { get; set; } = "";
    public string Approver { get; set; } = "";
    public string Executor { get; set; } = "";
    public string Reason { get; set; } = "";
    public bool BackupSkipped { get; set; }
    public bool BackupCleanupRequired { get; set; }
    public string BackupTopic { get; set; } = "";
    public string Failure { get; set; } = "";
    public string Evidence { get; set; } = "";
    public bool ManualChecklist { get; set; }
    public List<string> StopProducers { get; set; } = [];
    public List<string> StopConsumers { get; set; } = [];
    public List<KafkaMigrationTimelineEntry> Timeline { get; set; } = [];

    public string StepName => Step >= 0 && Step < StepNames.Length ? StepNames[Step] : Step.ToString();

    public static readonly string[] StepNames =
    [
        "Planned", "Pending", "Approved", "A1", "A2", "A3", "A4",
        "B1", "B2", "B3", "B4", "B5", "B6", "B7", "H1",
        "C1", "C2", "C3", "C4", "C5", "D1", "D2", "D3",
        "Done", "RollingBack", "RolledBack", "NeedsAttention", "Rejected"
    ];
}

public sealed class KafkaMigrationTimelineEntry
{
    public int Sequence { get; set; }
    public int Step { get; set; }
    public string Text { get; set; } = "";
    public DateTimeOffset At { get; set; }
}

public sealed class KafkaTopicDetail
{
    public string Topic { get; set; } = "";
    public string? Error { get; set; }
    public string TopicId { get; set; } = "";
    public int Partitions { get; set; }
    public int ReplicationFactor { get; set; }
    public bool FullIsr { get; set; }
    public bool LeadersSkewed { get; set; }
    public string KeyClass { get; set; } = "";
    public string KeyShape { get; set; } = "";
    public bool Slice1Eligible { get; set; }
    public string Eligibility { get; set; } = "";
    public List<string> Producers { get; set; } = [];
    public List<string> Consumers { get; set; } = [];
    public List<string> StopSet { get; set; } = [];
    public bool Held { get; set; }
    public long EstimatedRecords { get; set; }
    public bool PartitionDrift { get; set; }
    public List<KafkaPartitionFact> PartitionsDetail { get; set; } = [];
}

public sealed class KafkaPartitionFact
{
    public int Partition { get; set; }
    public int Leader { get; set; }
    public List<int> Replicas { get; set; } = [];
    public List<int> Isr { get; set; } = [];
    public bool PreferredLeader { get; set; }
    public long LogStart { get; set; }
    public long HighWatermark { get; set; }
}

public sealed class KafkaTopicConfigs
{
    public string Topic { get; set; } = "";
    public string Diff { get; set; } = "";
    public List<KafkaConfigRow> Configs { get; set; } = [];
    public List<string> Changes { get; set; } = [];
}

public sealed class KafkaConfigRow
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string Source { get; set; } = "";
}

public sealed class ReplicaMove
{
    public string Topic { get; set; } = "";
    public int Partition { get; set; }
    public int FromBroker { get; set; }
    public int ToBroker { get; set; }
    public List<int> Replicas { get; set; } = [];
}
