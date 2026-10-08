using System.Security.Claims;
using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Infrastructure;
using Link.Authorization.Permissions;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public interface IKafkaOpsService
{
    Task<KafkaTopicsResponse> GetTopicsAsync(CancellationToken cancellationToken);
    Task<KafkaGroupsResponse> GetGroupsAsync(bool includeTestGroups, CancellationToken cancellationToken);
    Task<KafkaCapabilitiesResponse> GetCapabilitiesAsync(CancellationToken cancellationToken);
    Task<PartitionPlan> PlanAsync(string topic, int requestedPartitions, bool overrideQuietWindow, string? overrideReason, CancellationToken cancellationToken);
    Task<ChangeRequestRecord> CreateAsync(ClaimsPrincipal user, string topic, int requestedPartitions, string reason, bool overrideQuietWindow, string? overrideReason, string? confirmation, string? correlationId, CancellationToken cancellationToken);
    Task<PartitionPlan> PlanFamilyAsync(string topic, bool overrideQuietWindow, string? overrideReason, CancellationToken cancellationToken);
    Task<ChangeRequestRecord> CreateFamilyAsync(ClaimsPrincipal user, string topic, string reason, bool overrideQuietWindow, string? overrideReason, string? confirmation, string? correlationId, CancellationToken cancellationToken);
    Task<ClusterSnapshot> GetClusterAsync(CancellationToken cancellationToken);
    Task<ReplicaScalePlan> PlanScaleAsync(string groupId, int desiredMembers, CancellationToken cancellationToken);
    Task<ChangeRequestRecord> CreateScaleAsync(ClaimsPrincipal user, string groupId, int desiredMembers, string reason, string? correlationId, CancellationToken cancellationToken);
    Task<BrokerMovePlan> PlanDecommissionAsync(int brokerId, CancellationToken cancellationToken);
    Task<ChangeRequestRecord> CreateDecommissionAsync(ClaimsPrincipal user, int brokerId, string reason, string? correlationId, CancellationToken cancellationToken);
    Task<BrokerMovePlan> PlanRebalanceAsync(int brokerId, CancellationToken cancellationToken);
    Task<ChangeRequestRecord> CreateRebalanceAsync(ClaimsPrincipal user, int brokerId, string reason, string? correlationId, CancellationToken cancellationToken);
    Task<InfraStatus> PlanAddBrokerAsync(CancellationToken cancellationToken);
    Task<ChangeRequestRecord> CreateAddBrokerAsync(ClaimsPrincipal user, string reason, string? correlationId, CancellationToken cancellationToken);
    Task<ChangeRequestRecord> CancelAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken);
    InfraStatus Infra { get; }
    bool CanScale(ClaimsPrincipal user);
    Task<ChangeRequestRecord> ApproveAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken);
    Task<ChangeRequestRecord> RejectAsync(ClaimsPrincipal user, Guid id, string reason, CancellationToken cancellationToken);
    Task<ChangeRequestRecord> ExecuteAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken);
    Task<ChangeRequestRecord?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task TrackAsync(CancellationToken cancellationToken);
    bool CanView(ClaimsPrincipal user);
    bool CanManage(ClaimsPrincipal user);
    bool ReadOnly { get; }
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
    public long TotalLag { get; set; }
    public bool LagKnown { get; set; } = true;
    public int MaxReplicas { get; set; }
    public List<string> Groups { get; set; } = [];
    public string? Error { get; set; }
}

public sealed class KafkaGroupsResponse
{
    public List<GroupView> Groups { get; set; } = [];
    public string? Error { get; set; }
}

public sealed class KafkaCapabilitiesResponse
{
    public List<TopicCapability> Topics { get; set; } = [];
    public string? Error { get; set; }
}

public sealed class TopicCapability
{
    public string Topic { get; set; } = "";
    public List<string> AuthorizedOperations { get; set; } = [];
    public bool? CanAlterPartitions { get; set; }
    public string? Error { get; set; }
}

public sealed class WatermarkSample
{
    public DateTimeOffset At { get; set; }
    public long Sum { get; set; }
}

public sealed partial class KafkaOpsService : IKafkaOpsService
{
    private readonly IKafkaBrokerGateway _broker;
    private readonly ICacheService _cache;
    private readonly IOptions<KafkaOpsOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly IProducer<string, AuditEventMessage>? _audit;
    private readonly IKafkaInfraProvider _infra;
    private readonly ILogger<KafkaOpsService> _logger;
    private readonly bool _anonymousAccess;
    private readonly bool _readOnly;

    public KafkaOpsService(
        IKafkaBrokerGateway broker,
        ICacheService cache,
        IOptions<KafkaOpsOptions> options,
        IHostEnvironment environment,
        ILogger<KafkaOpsService> logger,
        IConfiguration configuration,
        IEnumerable<IProducer<string, AuditEventMessage>> auditProducers,
        IKafkaInfraProvider infra)
    {
        _broker = broker;
        _cache = cache;
        _options = options;
        _environment = environment;
        _logger = logger;
        _infra = infra;
        _anonymousAccess = configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");
        _audit = auditProducers.FirstOrDefault();
        var readOnlySetting = configuration.GetSection(KafkaOpsOptions.SectionName)["ReadOnly"];
        _readOnly = readOnlySetting is null ? environment.IsProduction() : options.Value.ReadOnly;
    }

    public bool CanView(ClaimsPrincipal user) =>
        _anonymousAccess || Has(user, LinkSystemPermissions.CanViewInfrastructure);

    public bool CanManage(ClaimsPrincipal user) =>
        _anonymousAccess || Has(user, LinkSystemPermissions.CanManageKafkaTopics);

    public bool CanScale(ClaimsPrincipal user) =>
        _anonymousAccess || Has(user, LinkSystemPermissions.CanManageScaling);

    public InfraStatus Infra => new()
    {
        Provider = _infra.Name,
        Enabled = _infra.Enabled,
        Detail = _infra.Detail
    };

    public async Task<KafkaTopicsResponse> GetTopicsAsync(CancellationToken cancellationToken)
    {
        var cacheKey = Key("view:topics");
        var cached = await _cache.GetAsync<KafkaTopicsResponse>(cacheKey, cancellationToken);
        if (cached is not null && cached.Error is null && cached.Topics.Count > 0)
            return cached;

        var response = new KafkaTopicsResponse
        {
            Cap = _options.Value.MaxPartitionsPerTopic,
            ReadOnly = ReadOnly
        };
        try
        {
            var names = TopicNames();
            var described = await _broker.DescribeTopicsAsync(names, probeAlter: false, cancellationToken);
            var byName = described.ToDictionary(topic => topic.Topic, StringComparer.OrdinalIgnoreCase);
            var groups = await SafeGroupsAsync(includeTestGroups: false, cancellationToken);
            var groupsKnown = groups is not null;
            if (!groupsKnown)
                response.GroupsError = "Subscribed groups could not be read, so lag is unknown.";
            foreach (var entry in KafkaTopicCatalog.Topics)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byName.TryGetValue(entry.Topic, out var main);
                byName.TryGetValue(KafkaTopicCatalog.RetryName(entry.Topic), out var retry);
                byName.TryGetValue(KafkaTopicCatalog.ErrorName(entry.Topic), out var error);
                var partitions = main?.Partitions ?? 0;
                var retryPartitions = retry?.Partitions ?? 0;
                var rate = await ProduceRateAsync(entry.Topic, main?.HighWatermarks.Sum() ?? 0, cancellationToken);
                var lag = groupsKnown
                    ? groups!.Where(group => entry.Groups.Contains(group.GroupId, StringComparer.Ordinal))
                        .Sum(group => group.Partitions.Where(partition => partition.Topic == entry.Topic).Sum(partition => partition.Lag))
                    : 0;
                response.Topics.Add(new KafkaTopicRow
                {
                    Topic = entry.Topic,
                    Family = entry.Topic,
                    KeyClass = entry.KeyClass.ToString(),
                    KeyShape = entry.KeyShape,
                    HardBlocked = entry.HardBlocked,
                    OrderSensitive = entry.OrderSensitive,
                    Partitions = partitions,
                    RetryPartitions = retryPartitions,
                    ErrorPartitions = error?.Partitions ?? 0,
                    RetryBehind = retryPartitions > 0 && retryPartitions < partitions,
                    ErrorBehind = (error?.Partitions ?? 0) > 0 && (error?.Partitions ?? 0) < partitions,
                    ReplicationFactor = main?.ReplicationFactor ?? 0,
                    Configs = main?.Configs ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    ProduceRatePerSecond = rate,
                    TotalLag = lag,
                    LagKnown = groupsKnown,
                    MaxReplicas = partitions,
                    Groups = entry.Groups.ToList(),
                    Error = main?.Error
                });
            }
        }
        catch (Exception ex)
        {
            response.Error = "Kafka could not be read. " + ex.Message;
            _logger.LogWarning(ex, "Kafka topic view failed");
            return response;
        }

        if (response.GroupsError is null)
            await _cache.SetAsync(cacheKey, response, TimeSpan.FromSeconds(Math.Clamp(_options.Value.CacheSeconds, 5, 10)), ExpirationType.Absolute, cancellationToken);
        return response;
    }

    public async Task<KafkaGroupsResponse> GetGroupsAsync(bool includeTestGroups, CancellationToken cancellationToken)
    {
        var cacheKey = Key(includeTestGroups ? "view:groups:all" : "view:groups");
        var cached = await _cache.GetAsync<KafkaGroupsResponse>(cacheKey, cancellationToken);
        if (cached is not null && cached.Error is null)
            return cached;

        try
        {
            var groups = await _broker.DescribeGroupsAsync(includeTestGroups, _options.Value.ExpectedConfigVersion, cancellationToken);
            var response = new KafkaGroupsResponse { Groups = groups.ToList() };
            await _cache.SetAsync(cacheKey, response, TimeSpan.FromSeconds(Math.Clamp(_options.Value.CacheSeconds, 5, 10)), ExpirationType.Absolute, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kafka group view failed");
            return new KafkaGroupsResponse { Error = "Kafka groups could not be read. " + ex.Message };
        }
    }

    public async Task<KafkaCapabilitiesResponse> GetCapabilitiesAsync(CancellationToken cancellationToken)
    {
        var cacheKey = Key("view:capabilities");
        var cached = await _cache.GetAsync<KafkaCapabilitiesResponse>(cacheKey, cancellationToken);
        if (cached is not null && cached.Error is null && cached.Topics.Count > 0)
            return cached;

        try
        {
            var names = KafkaTopicCatalog.Topics.Select(topic => topic.Topic).ToList();
            var described = await _broker.DescribeTopicsAsync(names, probeAlter: true, cancellationToken);
            var response = new KafkaCapabilitiesResponse
            {
                Topics = described.Select(topic => new TopicCapability
                {
                    Topic = topic.Topic,
                    AuthorizedOperations = topic.AuthorizedOperations,
                    CanAlterPartitions = topic.CanAlterPartitions,
                    Error = topic.Error
                }).ToList()
            };
            if (response.Topics.Count > 0)
                await _cache.SetAsync(cacheKey, response, TimeSpan.FromSeconds(Math.Clamp(_options.Value.CacheSeconds, 5, 10)), ExpirationType.Absolute, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kafka capability probe failed");
            return new KafkaCapabilitiesResponse { Error = "The capability probe failed. " + ex.Message };
        }
    }

    public async Task<PartitionPlan> PlanAsync(string topic, int requestedPartitions, bool overrideQuietWindow, string? overrideReason, CancellationToken cancellationToken)
    {
        var (request, _) = await BuildPlanRequestAsync(topic, requestedPartitions, overrideQuietWindow, overrideReason, cancellationToken);
        return PartitionChangePlanner.Evaluate(request);
    }

    public async Task<PartitionPlan> PlanFamilyAsync(string topic, bool overrideQuietWindow, string? overrideReason, CancellationToken cancellationToken)
    {
        var (request, _) = await BuildPlanRequestAsync(topic, 0, overrideQuietWindow, overrideReason, cancellationToken);
        return PartitionChangePlanner.CompleteFamily(request);
    }

    public async Task<ChangeRequestRecord> CreateFamilyAsync(ClaimsPrincipal user, string topic, string reason, bool overrideQuietWindow, string? overrideReason, string? confirmation, string? correlationId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        EnsureActor(user, KafkaChangeKind.CompleteTopicFamily);
        if (string.IsNullOrWhiteSpace(reason))
            throw new KafkaOpsRejectedException("A reason is required.");

        var plan = await PlanFamilyAsync(topic, overrideQuietWindow, overrideReason, cancellationToken);
        if (!plan.Accepted)
            throw new KafkaOpsRejectedException(string.Join(" ", plan.Errors));
        if (!string.Equals((confirmation ?? "").Trim(), plan.Topic, StringComparison.Ordinal))
            throw new KafkaOpsRejectedException("Type the topic name to confirm. Adding partitions cannot be reversed.");

        var now = DateTimeOffset.UtcNow;
        var record = new ChangeRequestRecord
        {
            Id = Guid.NewGuid(),
            Kind = KafkaChangeKind.CompleteTopicFamily,
            Environment = _environment.EnvironmentName,
            Topic = plan.Topic,
            Family = plan.Family,
            RetryTopic = plan.RetryTopic,
            BeforePartitions = plan.CurrentPartitions,
            RequestedPartitions = plan.RequestedPartitions,
            MaxReplicas = plan.MaxReplicas,
            KeyClass = plan.KeyClass.ToString(),
            SecondApproverRequired = plan.SecondApproverRequired,
            QuietWindowOverride = plan.QuietWindowOverride,
            Reason = reason.Trim(),
            OverrideReason = overrideReason?.Trim() ?? "",
            Requester = UserName(user),
            DryRunSummary = plan.Summary,
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId.Trim(),
            Status = plan.SecondApproverRequired ? KafkaChangeStatus.Pending : KafkaChangeStatus.Approved,
            CreatedUtc = now,
            ApprovedUtc = plan.SecondApproverRequired ? null : now,
            AffectedGroups = plan.AffectedGroups
        };
        if (!plan.SecondApproverRequired)
            record.Approver = record.Requester;

        await SaveAsync(record, cancellationToken);
        await AuditAsync(record, "requested", cancellationToken);
        return record;
    }

    public async Task<ChangeRequestRecord> CreateAsync(ClaimsPrincipal user, string topic, int requestedPartitions, string reason, bool overrideQuietWindow, string? overrideReason, string? confirmation, string? correlationId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        if (string.IsNullOrWhiteSpace(reason))
            throw new KafkaOpsRejectedException("A reason is required.");

        var plan = await PlanAsync(topic, requestedPartitions, overrideQuietWindow, overrideReason, cancellationToken);
        if (!plan.Accepted)
            throw new KafkaOpsRejectedException(string.Join(" ", plan.Errors));
        if (!string.Equals((confirmation ?? "").Trim(), plan.Topic, StringComparison.Ordinal))
            throw new KafkaOpsRejectedException("Type the topic name to confirm. Adding partitions cannot be reversed.");

        var now = DateTimeOffset.UtcNow;
        var record = new ChangeRequestRecord
        {
            Id = Guid.NewGuid(),
            Environment = _environment.EnvironmentName,
            Topic = plan.Topic,
            Family = plan.Family,
            RetryTopic = plan.RetryTopic,
            BeforePartitions = plan.CurrentPartitions,
            RequestedPartitions = plan.RequestedPartitions,
            MaxReplicas = plan.MaxReplicas,
            KeyClass = plan.KeyClass.ToString(),
            SecondApproverRequired = plan.SecondApproverRequired,
            QuietWindowOverride = plan.QuietWindowOverride,
            Reason = reason.Trim(),
            OverrideReason = overrideReason?.Trim() ?? "",
            Requester = UserName(user),
            DryRunSummary = plan.Summary,
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId.Trim(),
            Status = plan.SecondApproverRequired ? KafkaChangeStatus.Pending : KafkaChangeStatus.Approved,
            CreatedUtc = now,
            ApprovedUtc = plan.SecondApproverRequired ? null : now,
            AffectedGroups = plan.AffectedGroups
        };
        if (!plan.SecondApproverRequired)
            record.Approver = record.Requester;

        await SaveAsync(record, cancellationToken);
        await AuditAsync(record, "requested", cancellationToken);
        return record;
    }

    public async Task<ChangeRequestRecord> ApproveAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var record = await RequireAsync(id, cancellationToken);
        EnsureActor(user, record.Kind);
        if (record.Kind is not (KafkaChangeKind.CancelReassignment or KafkaChangeKind.ScaleReplicas))
            await EnsureNoReassignmentAsync(record.Id, cancellationToken);
        var error = ChangeRequestWorkflow.Approve(record, UserName(user), DateTimeOffset.UtcNow);
        if (error is not null)
            throw new KafkaOpsRejectedException(error);
        await SaveAsync(record, cancellationToken);
        await AuditAsync(record, "approved", cancellationToken);
        return record;
    }

    public async Task<ChangeRequestRecord> RejectAsync(ClaimsPrincipal user, Guid id, string reason, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var record = await RequireAsync(id, cancellationToken);
        EnsureActor(user, record.Kind);
        var error = ChangeRequestWorkflow.Reject(record, UserName(user), reason, DateTimeOffset.UtcNow);
        if (error is not null)
            throw new KafkaOpsRejectedException(error);
        await SaveAsync(record, cancellationToken);
        if (OwnsRebalanceResource(record))
            await ReleaseRebalanceAsync(record, cancellationToken);
        await AuditAsync(record, "rejected", cancellationToken);
        return record;
    }

    public async Task<ChangeRequestRecord> ExecuteAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var record = await RequireAsync(id, cancellationToken);
        if (record.Kind == KafkaChangeKind.CancelReassignment)
            return await ExecuteCancelAsync(user, record, cancellationToken);
        if (record.Kind is not (KafkaChangeKind.PartitionIncrease or KafkaChangeKind.CompleteTopicFamily))
            return await ExecuteInfraAsync(user, record, cancellationToken);

        EnsureActor(user, record.Kind);
        await EnsureNoReassignmentAsync(record.Id, cancellationToken);
        var error = ChangeRequestWorkflow.MarkExecuting(record, UserName(user), DateTimeOffset.UtcNow);
        if (error is not null)
            throw new KafkaOpsRejectedException(error);

        try
        {
            var plan = await PlanExcludingAsync(record, cancellationToken);
            if (!plan.Accepted)
                throw new KafkaOpsRejectedException(string.Join(" ", plan.Errors));

            if (record.Kind == KafkaChangeKind.CompleteTopicFamily)
            {
                if (plan.TopicsToRaise.Contains(record.Topic, StringComparer.Ordinal))
                    throw new KafkaOpsRejectedException("The main topic cannot be changed by a family completion.");
                foreach (var sibling in plan.TopicsToRaise)
                    await _broker.IncreasePartitionsAsync(sibling, plan.RequestedPartitions, cancellationToken);
                record.RequestedPartitions = plan.RequestedPartitions;
            }
            else
            {
                await _broker.IncreasePartitionsAsync(record.Topic, record.RequestedPartitions, cancellationToken);
                record.PartitionsChangedUtc = DateTimeOffset.UtcNow;
                if (record.RetryTopic.Length > 0)
                    await _broker.IncreasePartitionsAsync(record.RetryTopic, record.RequestedPartitions, cancellationToken);
                await _broker.IncreasePartitionsAsync(KafkaTopicCatalog.ErrorName(record.Topic), record.RequestedPartitions, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            var reason = ex is KafkaOpsRejectedException
                ? ex.Message
                : "The partition increase failed. " + ex.Message;
            await PersistFailedAsync(record, reason, cancellationToken);
            throw new KafkaOpsRejectedException(reason);
        }

        record.Status = KafkaChangeStatus.Converging;
        record.MaxReplicas = record.RequestedPartitions;
        foreach (var group in record.AffectedGroups)
        {
            await _cache.SetAsync(Key("replica-cap:" + group), record.RequestedPartitions, TimeSpan.FromDays(365), ExpirationType.Absolute, cancellationToken);
        }

        await SaveAsync(record, cancellationToken);
        await AuditAsync(record, "executed", cancellationToken);
        return record;
    }

    public async Task<ChangeRequestRecord?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await _cache.GetAsync<ChangeRequestRecord>(RequestKey(id), cancellationToken);

    public async Task TrackAsync(CancellationToken cancellationToken)
    {
        var ids = await IdsAsync(cancellationToken);
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = await _cache.GetAsync<ChangeRequestRecord>(RequestKey(id), cancellationToken);
            if (record is null)
                continue;
            if (record.Status == KafkaChangeStatus.Converging && record.Kind is KafkaChangeKind.PartitionIncrease or KafkaChangeKind.CompleteTopicFamily)
                await TrackConvergenceAsync(record, cancellationToken);
            else if (record.Status == KafkaChangeStatus.Converging)
                await TrackInfraAsync(record, cancellationToken);
            else if (record.Status == KafkaChangeStatus.TimedOut && BlocksForReassignment(record))
                await TrackInfraAsync(record, cancellationToken);
            else if (record.Status == KafkaChangeStatus.Verifying)
                await TrackVerificationAsync(record, cancellationToken);
        }
    }

    private async Task TrackConvergenceAsync(ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        IReadOnlyList<GroupView> groups;
        try
        {
            groups = await _broker.DescribeGroupsAsync(includeTestGroups: false, _options.Value.ExpectedConfigVersion, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Convergence poll failed for {Topic}", record.Topic.SanitizeAndRemove());
            return;
        }

        record.Groups = [];
        var complete = true;
        foreach (var groupId in record.AffectedGroups)
        {
            var group = groups.FirstOrDefault(candidate => candidate.GroupId == groupId);
            var assigned = group?.Members
                .SelectMany(member => member.Assignment)
                .Where(item => AssignmentMatches(item, record.Topic) || AssignmentMatches(item, record.RetryTopic))
                .Distinct(StringComparer.Ordinal)
                .Count() ?? 0;
            var members = group?.Members.Count ?? 0;
            var expected = record.RequestedPartitions * (record.RetryTopic.Length > 0 ? 2 : 1);
            var groupComplete = members == 0 || assigned >= expected;
            if (!groupComplete)
                complete = false;
            record.Groups.Add(new GroupProgress
            {
                GroupId = groupId,
                AssignedPartitions = assigned,
                ExpectedPartitions = expected,
                Complete = groupComplete
            });
        }

        var started = record.ExecutedUtc ?? record.CreatedUtc;
        var limit = TimeSpan.FromMilliseconds(_options.Value.MetadataRefreshIntervalMs * Math.Max(1, _options.Value.ConvergenceAlertMultiple));
        if (!record.AlertRaised && DateTimeOffset.UtcNow - started > limit)
        {
            record.AlertRaised = true;
            await AuditAsync(record, "convergence-slow", cancellationToken);
        }

        if (complete)
        {
            record.Status = KafkaChangeStatus.Verifying;
            record.ConvergedUtc = DateTimeOffset.UtcNow;
            await AuditAsync(record, "converged", cancellationToken);
        }

        await SaveAsync(record, cancellationToken);
    }

    private async Task TrackVerificationAsync(ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        var groups = await SafeGroupsAsync(includeTestGroups: false, cancellationToken);
        var started = record.ConvergedUtc ?? DateTimeOffset.UtcNow;
        var windowElapsed = DateTimeOffset.UtcNow - started >= TimeSpan.FromSeconds(Math.Max(1, _options.Value.VerificationWindowSeconds));
        if (groups is null)
        {
            if (!windowElapsed)
                return;

            record.Status = KafkaChangeStatus.NeedsAttention;
            record.Failure = "Consumer lag could not be read during the verification window.";
            record.ClosedUtc = DateTimeOffset.UtcNow;
            await SaveAsync(record, cancellationToken);
            await AuditAsync(record, "needs-attention", cancellationToken);
            return;
        }

        var affected = groups.Where(group => record.AffectedGroups.Contains(group.GroupId, StringComparer.Ordinal)).ToList();
        var lag = affected.Sum(group => group.Partitions
            .Where(partition => partition.Topic == record.Family || partition.Topic == record.RetryTopic)
            .Sum(partition => partition.Lag));
        var checkpoints = SnapshotOffsets(affected, record.Family, record.RetryTopic);
        var errorSum = await ErrorWatermarkAsync(record.Family, cancellationToken);

        if (!record.BaselineCaptured)
        {
            record.BaselineCaptured = true;
            record.BaselineLag = lag;
            record.BaselineOffsets = checkpoints;
            record.ErrorWatermarkAtStart = errorSum ?? -1;
        }
        else if (errorSum is long sum)
        {
            if (record.ErrorWatermarkAtStart < 0)
                record.ErrorWatermarkAtStart = sum;
            else if (sum > record.ErrorWatermarkAtStart)
                record.ErrorTopicAdvanced = true;
        }

        record.LatestOffsets = checkpoints;
        if (!windowElapsed)
        {
            await SaveAsync(record, cancellationToken);
            return;
        }

        var stalled = record.BaselineOffsets.Any(start =>
        {
            var latest = record.LatestOffsets.FirstOrDefault(item => item.Topic == start.Topic && item.Partition == start.Partition);
            return latest is not null
                && latest.HighWatermark > start.HighWatermark
                && latest.Committed == start.Committed;
        });
        var reasons = new List<string>();
        if (lag > record.BaselineLag)
            reasons.Add($"Lag is {lag}, above the {record.BaselineLag} baseline recorded at convergence.");
        if (stalled)
            reasons.Add("A partition high watermark advanced while its committed offset stayed still.");
        if (record.ErrorTopicAdvanced)
            reasons.Add("The error topic received messages during the verification window.");

        if (reasons.Count > 0)
        {
            record.Status = KafkaChangeStatus.NeedsAttention;
            record.Failure = string.Join(" ", reasons);
        }
        else
        {
            record.Status = KafkaChangeStatus.Done;
            record.Failure = "";
        }

        record.ClosedUtc = DateTimeOffset.UtcNow;
        await SaveAsync(record, cancellationToken);
        await AuditAsync(record, record.Status == KafkaChangeStatus.Done ? "verified" : "needs-attention", cancellationToken);
    }

    private static List<OffsetCheckpoint> SnapshotOffsets(List<GroupView> groups, string family, string retryTopic)
    {
        return groups.SelectMany(group => group.Partitions)
            .Where(partition => partition.Topic == family || partition.Topic == retryTopic)
            .GroupBy(partition => partition.Topic + "\n" + partition.Partition)
            .Select(group => group.First())
            .Select(partition => new OffsetCheckpoint
            {
                Topic = partition.Topic,
                Partition = partition.Partition,
                Committed = partition.Committed,
                HighWatermark = partition.HighWatermark
            })
            .ToList();
    }

    private async Task<long?> ErrorWatermarkAsync(string family, CancellationToken cancellationToken)
    {
        try
        {
            var described = await _broker.DescribeTopicsAsync([KafkaTopicCatalog.ErrorName(family)], probeAlter: false, cancellationToken);
            var topic = described.FirstOrDefault();
            if (topic is null || topic.Error is not null)
                return null;
            return topic.HighWatermarks.Sum();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error topic watermark skipped");
            return null;
        }
    }

    private async Task<PartitionPlan> PlanExcludingAsync(ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        var (request, _) = await BuildPlanRequestAsync(
            record.Topic,
            record.RequestedPartitions,
            record.QuietWindowOverride,
            record.OverrideReason,
            cancellationToken,
            record.Id);
        return record.Kind == KafkaChangeKind.CompleteTopicFamily
            ? PartitionChangePlanner.CompleteFamily(request)
            : PartitionChangePlanner.Evaluate(request);
    }

    private async Task PersistFailedAsync(ChangeRequestRecord record, string reason, CancellationToken cancellationToken)
    {
        record.Status = KafkaChangeStatus.Failed;
        record.Failure = reason;
        record.ClosedUtc = DateTimeOffset.UtcNow;
        await SaveAsync(record, cancellationToken);
        if (OwnsRebalanceResource(record))
            await ReleaseRebalanceAsync(record, cancellationToken);
        await AuditAsync(record, "execution-failed", cancellationToken);
    }

    private async Task<(PartitionPlanRequest Request, KafkaTopicsResponse Topics)> BuildPlanRequestAsync(
        string topic,
        int requestedPartitions,
        bool overrideQuietWindow,
        string? overrideReason,
        CancellationToken cancellationToken,
        Guid? excludeRequestId = null)
    {
        var topics = await GetTopicsAsync(cancellationToken);
        if (topics.Error is not null)
            throw new KafkaOpsRejectedException(topics.Error);

        var requestedName = (topic ?? "").Trim();
        var family = KafkaTopicCatalog.MainName(requestedName);
        var row = topics.Topics.FirstOrDefault(candidate => string.Equals(candidate.Topic, family, StringComparison.OrdinalIgnoreCase));
        var groups = await SafeGroupsAsync(includeTestGroups: false, cancellationToken);
        if (groups is null)
            throw new KafkaOpsRejectedException("Subscribed groups could not be read, so lag and member config were not checked. The plan is refused.");
        var subscribed = KafkaTopicCatalog.GroupsOf(family);
        var snapshots = new List<GroupSafetySnapshot>();
        var lag = 0L;
        foreach (var groupId in subscribed)
        {
            var group = groups.FirstOrDefault(candidate => candidate.GroupId == groupId);
            var groupLag = group?.Partitions.Where(partition => partition.Topic == family || partition.Topic == KafkaTopicCatalog.RetryName(family)).Sum(partition => partition.Lag) ?? 0;
            lag += groupLag;
            snapshots.Add(new GroupSafetySnapshot
            {
                GroupId = groupId,
                MemberCount = group?.Members.Count ?? 0,
                MembersOnExpectedConfig = group?.MembersOnExpectedConfig ?? 0,
                TotalLag = groupLag
            });
        }

        var quiet = await QuietWindowAsync(family, row?.ProduceRatePerSecond ?? 0, lag, cancellationToken);
        var records = await OpenRecordsAsync(cancellationToken);
        if (excludeRequestId is Guid excluded)
            records = records.Where(record => record.Id != excluded).ToList();
        var now = DateTimeOffset.UtcNow;
        var request = new PartitionPlanRequest
        {
            Topic = requestedName,
            CurrentPartitions = row?.Partitions ?? 0,
            RetryTopicExists = row is { RetryPartitions: > 0 },
            RetryPartitions = row?.RetryPartitions ?? 0,
            ErrorTopicExists = row is { ErrorPartitions: > 0 },
            ErrorPartitions = row?.ErrorPartitions ?? 0,
            RequestedPartitions = requestedPartitions,
            Cap = _options.Value.MaxPartitionsPerTopic,
            QuietWindowMet = quiet.Met,
            QuietWindowDetail = quiet.Detail,
            OverrideQuietWindow = overrideQuietWindow,
            OverrideReason = overrideReason,
            RequireSecondApprover = _options.Value.RequireSecondApprover,
            EnvironmentChangeInFlight = records.Any(Open),
            FamilyChangeInFlight = records.Any(record => Open(record) && string.Equals(record.Family, family, StringComparison.OrdinalIgnoreCase)),
            LastFamilyChangeUtc = records
                .Where(record => record.Kind != KafkaChangeKind.CompleteTopicFamily && string.Equals(record.Family, family, StringComparison.OrdinalIgnoreCase) && record.PartitionsChangedUtc is not null)
                .Select(record => record.PartitionsChangedUtc)
                .OrderByDescending(value => value)
                .FirstOrDefault(),
            RateLimitMinutes = _options.Value.RateLimitMinutes,
            InFlightReassignmentTopics = (await InFlightReassignmentTopicsAsync(excludeRequestId, cancellationToken)).ToList(),
            Now = now,
            ExpectedConfigVersion = _options.Value.ExpectedConfigVersion,
            MetadataRefreshIntervalMs = _options.Value.MetadataRefreshIntervalMs,
            Groups = snapshots
        };
        return (request, topics);
    }

    private async Task<(bool Met, string Detail)> QuietWindowAsync(string topic, double rate, long lag, CancellationToken cancellationToken)
    {
        if (lag > 0)
            return (false, $"Subscribed lag is {lag}.");
        if (rate > 0)
            return (false, $"Produce rate is {rate:0.###} messages/second.");

        var samples = await _cache.GetAsync<List<WatermarkSample>>(Key("samples:" + topic), cancellationToken) ?? [];
        if (samples.Count == 0)
            return (false, "Produce rate has not been observed for long enough.");

        var window = TimeSpan.FromMilliseconds(_options.Value.MetadataRefreshIntervalMs * 2);
        var oldest = samples.Min(sample => sample.At);
        if (DateTimeOffset.UtcNow - oldest < window)
            return (false, "Produce rate has been observed for less than twice the metadata refresh.");

        var baseline = samples[0].Sum;
        if (samples.Any(sample => sample.Sum != baseline))
            return (false, "The high watermark moved during the quiet window.");

        return (true, "Lag is zero and the high watermark has been unchanged for the quiet window.");
    }

    private async Task<double> ProduceRateAsync(string topic, long sum, CancellationToken cancellationToken)
    {
        var key = Key("samples:" + topic);
        var samples = await _cache.GetAsync<List<WatermarkSample>>(key, cancellationToken) ?? [];
        var now = DateTimeOffset.UtcNow;
        samples.Add(new WatermarkSample { At = now, Sum = sum });
        samples = samples.Where(sample => now - sample.At < TimeSpan.FromMinutes(10)).ToList();
        await _cache.SetAsync(key, samples, TimeSpan.FromMinutes(15), ExpirationType.Absolute, cancellationToken);
        if (samples.Count < 2)
            return 0;
        var first = samples[0];
        var last = samples[^1];
        var seconds = (last.At - first.At).TotalSeconds;
        if (seconds <= 0)
            return 0;
        return Math.Max(0, (last.Sum - first.Sum) / seconds);
    }

    private async Task<List<GroupView>?> SafeGroupsAsync(bool includeTestGroups, CancellationToken cancellationToken)
    {
        try
        {
            var response = await GetGroupsUncachedAsync(includeTestGroups, cancellationToken);
            return response.Error is null ? response.Groups : null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Group snapshot skipped");
            return null;
        }
    }

    private async Task<KafkaGroupsResponse> GetGroupsUncachedAsync(bool includeTestGroups, CancellationToken cancellationToken)
    {
        var groups = await _broker.DescribeGroupsAsync(includeTestGroups, _options.Value.ExpectedConfigVersion, cancellationToken);
        return new KafkaGroupsResponse { Groups = groups.ToList() };
    }

    private async Task<List<ChangeRequestRecord>> OpenRecordsAsync(CancellationToken cancellationToken)
    {
        var records = new List<ChangeRequestRecord>();
        foreach (var id in await IdsAsync(cancellationToken))
        {
            var record = await _cache.GetAsync<ChangeRequestRecord>(RequestKey(id), cancellationToken);
            if (record is not null)
                records.Add(record);
        }

        return records;
    }

    private async Task SaveAsync(ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        await _cache.SetAsync(RequestKey(record.Id), record, TimeSpan.FromHours(24), ExpirationType.Absolute, cancellationToken);
        var ids = await IdsAsync(cancellationToken);
        if (!ids.Contains(record.Id))
            ids.Add(record.Id);
        await _cache.SetAsync(Key("index"), ids, TimeSpan.FromHours(24), ExpirationType.Absolute, cancellationToken);
    }

    private async Task<List<Guid>> IdsAsync(CancellationToken cancellationToken) =>
        await _cache.GetAsync<List<Guid>>(Key("index"), cancellationToken) ?? [];

    private async Task<ChangeRequestRecord> RequireAsync(Guid id, CancellationToken cancellationToken)
    {
        var record = await GetAsync(id, cancellationToken);
        if (record is null)
            throw new KafkaOpsNotFoundException("That change request was not found or it has expired.");
        return record;
    }

    private async Task AuditAsync(ChangeRequestRecord record, string action, CancellationToken cancellationToken)
    {
        if (_audit is null)
        {
            _logger.LogInformation("Kafka change {Action} for {Topic} has no audit producer", action, record.Topic.SanitizeAndRemove());
            return;
        }

        var message = new AuditEventMessage
        {
            ServiceName = "LinkAdminBFF",
            CorrelationId = record.CorrelationId,
            EventDate = DateTime.UtcNow,
            User = record.Approver.Length > 0 ? record.Approver : record.Requester,
            UserId = record.Approver.Length > 0 ? record.Approver : record.Requester,
            Action = action is "rejected" ? AuditEventType.Delete : AuditEventType.Update,
            Resource = record.Family,
            Notes = action + " " + record.DryRunSummary,
            PropertyChanges =
            [
                new PropertyChangeModel("partitions", record.BeforePartitions.ToString(), record.RequestedPartitions.ToString()),
                new PropertyChangeModel("status", "", record.Status.ToString()),
                new PropertyChangeModel("reason", "", record.Reason),
                new PropertyChangeModel("environment", "", record.Environment),
                new PropertyChangeModel("approver", "", record.Approver),
                new PropertyChangeModel("action", "", action)
            ]
        };

        try
        {
            var headers = new Headers();
            headers.Add("X-Correlation-Id", Encoding.ASCII.GetBytes(record.CorrelationId));
            await _audit.ProduceAsync(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
            {
                Key = record.Family,
                Value = message,
                Headers = headers
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kafka change audit event was not produced for {Topic}", record.Topic.SanitizeAndRemove());
        }
    }

    public bool ReadOnly => _readOnly;

    private static bool AssignmentMatches(string assignment, string topic)
    {
        if (topic.Length == 0 || assignment.Length <= topic.Length + 1)
            return false;
        if (!assignment.StartsWith(topic + "-", StringComparison.Ordinal))
            return false;
        return int.TryParse(assignment.AsSpan(topic.Length + 1), out _);
    }

    private void EnsureWritable()
    {
        if (ReadOnly)
            throw new KafkaOpsForbiddenException("Kafka changes are read-only in this environment.");
    }

    private string Key(string suffix) => "kafka-ops:" + _environment.EnvironmentName + ":" + suffix;

    private string RequestKey(Guid id) => Key("request:" + id.ToString("N"));

    private static bool Open(ChangeRequestRecord record) =>
        record.Status is KafkaChangeStatus.Pending or KafkaChangeStatus.Approved or KafkaChangeStatus.Executing or KafkaChangeStatus.Converging or KafkaChangeStatus.Verifying;

    private static List<string> TopicNames()
    {
        var names = new List<string>();
        foreach (var topic in KafkaTopicCatalog.Topics)
        {
            names.Add(topic.Topic);
            names.Add(KafkaTopicCatalog.RetryName(topic.Topic));
            names.Add(KafkaTopicCatalog.ErrorName(topic.Topic));
        }

        return names;
    }

    public static bool Has(ClaimsPrincipal user, LinkSystemPermissions permission) =>
        user.HasClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, permission.ToString());

    public static string UserName(ClaimsPrincipal user)
    {
        var name = user.Identity?.Name
            ?? user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue("preferred_username")
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(name) ? "anonymous" : name.Trim();
    }
}

public class KafkaOpsRejectedException : Exception
{
    public KafkaOpsRejectedException(string message) : base(message) { }
}

public sealed class KafkaOpsForbiddenException : KafkaOpsRejectedException
{
    public KafkaOpsForbiddenException(string message) : base(message) { }
}

public sealed class KafkaOpsNotFoundException : Exception
{
    public KafkaOpsNotFoundException(string message) : base(message) { }
}
