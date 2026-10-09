using System.Text.Json;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace Link.UI.Services;

/// <summary>
/// Recorded operations responses for the Kafka page when no broker is attached.
/// LinkUi:KafkaOpsFixture turns it on. Replica and broker changes stay disabled.
/// A replication-factor change is a partition reassignment, so a safe plan is applied here:
/// throttle, reassignment, ISR, then the throttle is cleared.
/// </summary>
public sealed class KafkaOpsFixture
{
    public const string DisabledDetail =
        "Broker and replica changes are disabled. DevOps must enable an infrastructure provider before replicas or brokers can change.";

    private readonly FixtureDocument _document;
    private readonly Dictionary<Guid, ChangeRequestRecord> _requests = new();
    private readonly Dictionary<string, List<KafkaBrowseRecord>> _produced = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public KafkaOpsFixture(IConfiguration configuration, IWebHostEnvironment environment)
    {
        Active = configuration.GetValue("LinkUi:KafkaOpsFixture", false);
        if (!Active)
        {
            _document = new FixtureDocument();
            return;
        }

        var path = Path.Combine(environment.ContentRootPath, "Fixtures", "kafka-ops.json");
        var json = File.ReadAllText(path);
        _document = JsonSerializer.Deserialize<FixtureDocument>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new FixtureDocument();
        foreach (var request in _document.Requests)
            _requests[request.Id] = request;
    }

    public bool Active { get; }

    public KafkaOpsCall<KafkaTopicsResponse> Topics() => Ok(_document.Topics);

    public KafkaOpsCall<KafkaGroupsResponse> Groups(bool includeTestGroups)
    {
        var response = new KafkaGroupsResponse
        {
            Groups = _document.Groups.Groups.ToList(),
            Error = _document.Groups.Error
        };
        if (includeTestGroups)
        {
            response.Groups.Add(new KafkaGroupRow
            {
                GroupId = "e2e-diag-sample",
                State = "Empty",
                TotalLag = 0
            });
        }

        return Ok(response);
    }

    public KafkaOpsCall<KafkaCapabilitiesResponse> Capabilities() => Ok(_document.Capabilities);

    public KafkaOpsCall<ClusterSnapshot> Cluster() => Ok(_document.Cluster);

    public KafkaOpsCall<InfraStatus> Infra() => Ok(_document.Infra);

    public KafkaOpsCall<KafkaTopicDetail> Detail(string topic)
    {
        var row = FindTopic(topic);
        if (row is null)
            return Fail<KafkaTopicDetail>("That topic was not found.");

        return Ok(new KafkaTopicDetail
        {
            Topic = row.Topic,
            TopicId = "6f1c2a90-7b14-4d2e-9a33-1c8e5b0d4f21",
            Partitions = row.Partitions,
            ReplicationFactor = row.ReplicationFactor,
            FullIsr = row.FullIsr,
            LeadersSkewed = row.LeadersSkewed,
            KeyClass = row.KeyClass,
            KeyShape = row.KeyShape,
            Slice1Eligible = row.Slice1Eligible,
            Eligibility = row.MigrationEligibility,
            Consumers = row.Groups.ToList(),
            PartitionsDetail = _document.Cluster.Placements
                .Where(item => string.Equals(item.Topic, row.Topic, StringComparison.Ordinal))
                .OrderBy(item => item.Partition)
                .Select(item => new KafkaPartitionFact
                {
                    Partition = item.Partition,
                    Leader = item.Leader,
                    Replicas = item.Replicas.ToList(),
                    Isr = item.Isr.ToList(),
                    PreferredLeader = item.Replicas.Count > 0 && item.Leader == item.Replicas[0],
                    LogStart = 0,
                    HighWatermark = Math.Max(0, 40 - (item.Partition * 10))
                })
                .ToList()
        });
    }

    public KafkaOpsCall<KafkaTopicConfigs> Configs(string topic, string? diff)
    {
        var row = FindTopic(topic);
        if (row is null)
            return Fail<KafkaTopicConfigs>("That topic was not found.");

        return Ok(new KafkaTopicConfigs
        {
            Topic = row.Topic,
            Diff = diff ?? "",
            Configs = row.Configs
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new KafkaConfigRow { Name = item.Key, Value = item.Value, Source = "fixture" })
                .ToList()
        });
    }

    public KafkaOpsCall<PartitionPlan> PlanPartitions(string topic, int partitions, bool overrideQuietWindow, string? overrideReason)
    {
        var row = FindTopic(topic);
        var plan = new PartitionPlan
        {
            Topic = topic,
            Family = row?.Family ?? topic,
            RetryTopic = topic + "-Retry",
            ErrorTopic = topic + "-Error",
            KeyClass = row?.KeyClass ?? "Facility",
            KeyShape = row?.KeyShape ?? "{facilityId}",
            HardBlocked = row?.HardBlocked == true,
            CurrentPartitions = row?.Partitions ?? 0,
            RequestedPartitions = partitions,
            MaxReplicas = partitions,
            Irreversible = true,
            AffectedGroups = row?.Groups.ToList() ?? []
        };
        plan.QuietWindowRequired = row is null || row.OrderSensitive;
        plan.QuietWindowMet = row is not null && row.TotalLag == 0 && row.ProduceRatePerSecond <= 0;
        if (row is null)
            plan.Errors.Add("The topic is not in the catalog.");
        if (row?.HardBlocked == true)
            plan.Errors.Add("Partition changes for this topic are blocked.");
        if (row is not null && partitions <= row.Partitions)
            plan.Errors.Add("Partitions can only increase.");
        if (partitions > Math.Max(1, _document.Topics.Cap))
            plan.Errors.Add("The requested count is above the environment cap.");
        if (plan.QuietWindowRequired && !plan.QuietWindowMet && !overrideQuietWindow)
            plan.Errors.Add("This topic is order-sensitive. Every subscribed group needs zero lag, and the produce rate must be zero, before partitions can increase.");
        if (overrideQuietWindow && string.IsNullOrWhiteSpace(overrideReason))
            plan.Errors.Add("An override reason is required.");
        plan.Notes.Add("Adding partitions is permanent and cannot be reversed. Keys are remapped.");
        plan.Notes.Add("The main topic, its retry topic, and its error topic increase together. None of them can shrink.");
        if (string.Equals(plan.KeyClass, "Facility", StringComparison.Ordinal))
            plan.Notes.Add("One facility can never use more than one partition. A quiet window keeps in-flight keys on the partition they already use.");
        else if (string.Equals(plan.KeyClass, "Patient", StringComparison.Ordinal))
            plan.Notes.Add("A patient key is {facilityId}:{patientId}. Raising the partition count remaps which partition that patient uses.");
        plan.SecondApproverRequired = !plan.HardBlocked;
        plan.Accepted = plan.Errors.Count == 0;
        plan.Summary = plan.Accepted ? string.Join(" ", plan.Notes) : string.Join(" ", plan.Errors);
        return plan.Accepted ? Ok(plan) : Bad(plan);
    }

    public KafkaOpsCall<ChangeRequestRecord> CreatePartitions(string topic, int partitions, string reason, bool overrideQuietWindow, string? overrideReason, string? confirmation, string correlationId)
    {
        if (!string.Equals((confirmation ?? "").Trim(), topic, StringComparison.Ordinal))
            return Fail<ChangeRequestRecord>("Type the topic name to confirm. Adding partitions cannot be reversed.");
        if (string.IsNullOrWhiteSpace(reason))
            return Fail<ChangeRequestRecord>("A reason is required.");
        if (_document.Topics.ReadOnly)
            return Fail<ChangeRequestRecord>("Kafka changes are read-only in this environment.");

        var plan = PlanPartitions(topic, partitions, overrideQuietWindow, overrideReason);
        if (plan.Value is not { Accepted: true })
            return Fail<ChangeRequestRecord>(plan.Error ?? plan.Value?.Summary ?? "The dry run was refused.");

        var record = new ChangeRequestRecord
        {
            Id = Guid.NewGuid(),
            Kind = "PartitionIncrease",
            Topic = topic,
            Family = plan.Value.Family,
            RetryTopic = plan.Value.RetryTopic,
            BeforePartitions = plan.Value.CurrentPartitions,
            RequestedPartitions = partitions,
            MaxReplicas = partitions,
            KeyClass = plan.Value.KeyClass,
            SecondApproverRequired = plan.Value.SecondApproverRequired,
            Reason = reason.Trim(),
            OverrideReason = overrideReason?.Trim() ?? "",
            Requester = "anonymous",
            DryRunSummary = plan.Value.Summary,
            CorrelationId = correlationId,
            Status = plan.Value.SecondApproverRequired ? "Pending" : "Approved",
            Approver = plan.Value.SecondApproverRequired ? "" : "anonymous",
            CreatedUtc = DateTimeOffset.UtcNow,
            Groups = AffectedGroupsCopied(plan.Value.AffectedGroups)
        };
        if (!plan.Value.SecondApproverRequired)
            record.ApprovedUtc = record.CreatedUtc;
        lock (_gate)
            _requests[record.Id] = record;
        return Ok(record);
    }

    public KafkaOpsCall<PartitionPlan> PlanFamily(string topic, bool overrideQuietWindow, string? overrideReason)
    {
        var row = FindTopic(topic);
        var plan = new PartitionPlan
        {
            Topic = topic,
            Family = row?.Family ?? topic,
            FamilyCompletion = true,
            RetryTopic = topic + "-Retry",
            ErrorTopic = topic + "-Error",
            KeyClass = row?.KeyClass ?? "Facility",
            KeyShape = row?.KeyShape ?? "{facilityId}",
            CurrentPartitions = row?.Partitions ?? 0,
            RequestedPartitions = row?.Partitions ?? 0,
            MaxReplicas = row?.Partitions ?? 0,
            AffectedGroups = row?.Groups.ToList() ?? []
        };
        if (row is null)
            plan.Errors.Add("The topic is not in the catalog.");
        else
        {
            if (row.RetryPartitions > 0 && row.RetryPartitions < row.Partitions)
                plan.TopicsToRaise.Add(plan.RetryTopic);
            if (row.ErrorPartitions > 0 && row.ErrorPartitions < row.Partitions)
                plan.TopicsToRaise.Add(plan.ErrorTopic);
            if (plan.TopicsToRaise.Count == 0)
                plan.Errors.Add("No sibling is behind the main topic.");
        }

        plan.Notes.Add("This catches sibling topics up to the main topic. The main topic is not changed.");
        plan.Accepted = plan.Errors.Count == 0;
        plan.Summary = plan.Accepted ? string.Join(" ", plan.Notes) : string.Join(" ", plan.Errors);
        return plan.Accepted ? Ok(plan) : Bad(plan);
    }

    public KafkaOpsCall<ChangeRequestRecord> CreateFamily(string topic, string reason, bool overrideQuietWindow, string? overrideReason, string? confirmation, string correlationId)
    {
        if (!string.Equals((confirmation ?? "").Trim(), topic, StringComparison.Ordinal))
            return Fail<ChangeRequestRecord>("Type the topic name to confirm. Adding partitions cannot be reversed.");
        if (string.IsNullOrWhiteSpace(reason))
            return Fail<ChangeRequestRecord>("A reason is required.");
        var plan = PlanFamily(topic, overrideQuietWindow, overrideReason);
        if (plan.Value is not { Accepted: true })
            return Fail<ChangeRequestRecord>(plan.Error ?? plan.Value?.Summary ?? "The dry run was refused.");
        return Store(new ChangeRequestRecord
        {
            Id = Guid.NewGuid(),
            Kind = "CompleteTopicFamily",
            Topic = topic,
            Family = plan.Value.Family,
            RetryTopic = plan.Value.RetryTopic,
            BeforePartitions = plan.Value.CurrentPartitions,
            RequestedPartitions = plan.Value.RequestedPartitions,
            MaxReplicas = plan.Value.MaxReplicas,
            Reason = reason.Trim(),
            Requester = "anonymous",
            Status = "Pending",
            SecondApproverRequired = true,
            DryRunSummary = plan.Value.Summary,
            CorrelationId = correlationId,
            CreatedUtc = DateTimeOffset.UtcNow
        });
    }

    public KafkaOpsCall<ReplicationFactorPlan> PlanReplicationFactor(string topic, int target, long throttle, string? search, int page, int pageSize)
    {
        lock (_gate)
            return PlanReplicationFactorUnlocked(topic, target, throttle, search, page, pageSize);
    }

    public KafkaOpsCall<ChangeRequestRecord> CreateReplicationFactor(string topic, int target, long throttle, string reason, string? confirmation, string correlationId)
    {
        if (!string.Equals((confirmation ?? "").Trim(), topic, StringComparison.Ordinal))
            return Fail<ChangeRequestRecord>("Type the topic name to confirm. The replication factor changes only after the request.");
        if (string.IsNullOrWhiteSpace(reason))
            return Fail<ChangeRequestRecord>("A reason is required.");
        lock (_gate)
        {
            if (_document.Topics.ReadOnly)
                return Fail<ChangeRequestRecord>("Kafka changes are read-only in this environment.");

            var evaluation = EvaluateReplication(topic, target, throttle, "", 1, 25);
            if (!evaluation.Plan.Accepted)
                return Fail<ChangeRequestRecord>(evaluation.Plan.Summary);

            foreach (var row in evaluation.Assignments)
            {
                var placement = _document.Cluster.Placements.FirstOrDefault(item =>
                    string.Equals(item.Topic, topic, StringComparison.Ordinal) && item.Partition == row.Partition);
                if (placement is null)
                    continue;
                placement.Leader = row.Leader;
                placement.Replicas = row.After.ToList();
                placement.Isr = row.After.ToList();
            }

            var topicRow = FindTopic(topic);
            if (topicRow is not null)
                topicRow.ReplicationFactor = target;

            var steps = ReplicationFactorRules.CompletedSteps(evaluation.Plan.ThrottleBytesPerSecond, evaluation.Plan.PartitionCount).ToList();
            var now = DateTimeOffset.UtcNow;
            return Store(new ChangeRequestRecord
            {
                Id = Guid.NewGuid(),
                Kind = "ReplicationFactor",
                Topic = topic,
                Family = topicRow?.Family ?? topic,
                BeforeReplicationFactor = evaluation.Plan.CurrentFactor,
                TargetReplicationFactor = target,
                ThrottleBytesPerSecond = evaluation.Plan.ThrottleBytesPerSecond,
                ThrottleCleared = true,
                BeforePartitions = topicRow?.Partitions ?? evaluation.Plan.PartitionCount,
                RequestedPartitions = topicRow?.Partitions ?? evaluation.Plan.PartitionCount,
                Reason = reason.Trim(),
                Requester = "anonymous",
                Status = "Done",
                DryRunSummary = evaluation.Plan.Summary,
                Progress = "Throttle cleared. Every replica is in the ISR.",
                Steps = steps,
                CorrelationId = correlationId,
                CreatedUtc = now,
                ExecutedUtc = now,
                ConvergedUtc = now,
                ClosedUtc = now
            });
        }
    }

    private KafkaOpsCall<ReplicationFactorPlan> PlanReplicationFactorUnlocked(string topic, int target, long throttle, string? search, int page, int pageSize)
    {
        var evaluation = EvaluateReplication(topic, target, throttle, search, page, pageSize);
        return evaluation.Plan.Accepted ? Ok(evaluation.Plan) : Bad(evaluation.Plan);
    }

    private ReplicationFactorEvaluation EvaluateReplication(string topic, int target, long throttle, string? search, int page, int pageSize)
    {
        var row = FindTopic(topic);
        var minIsr = 1;
        var configured = false;
        if (row is not null && row.Configs.TryGetValue("min.insync.replicas", out var raw))
        {
            configured = true;
            if (int.TryParse(raw, out var parsed) && parsed > 0)
                minIsr = parsed;
        }

        if (row is null)
        {
            var missing = new ReplicationFactorPlan
            {
                Topic = topic,
                TargetFactor = target,
                ThrottleBytesPerSecond = throttle
            };
            missing.Errors.Add("The topic is not in the catalog.");
            missing.Summary = missing.Errors[0];
            return new ReplicationFactorEvaluation { Plan = missing };
        }

        if (configured && minIsr == 1 && row.Configs.TryGetValue("min.insync.replicas", out var text) && !int.TryParse(text, out _))
        {
            var invalid = new ReplicationFactorPlan { Topic = topic, TargetFactor = target };
            invalid.Errors.Add("min.insync.replicas is not a number.");
            invalid.Summary = invalid.Errors[0];
            return new ReplicationFactorEvaluation { Plan = invalid };
        }

        return ReplicationFactorRules.Evaluate(
            topic,
            target,
            throttle,
            minIsr,
            configured,
            _document.Cluster.Brokers,
            _document.Cluster.Placements,
            search,
            page,
            pageSize);
    }

    public KafkaOpsCall<ReplicaScalePlan> PlanScale(string groupId, int replicas)
    {
        var group = _document.Groups.Groups.FirstOrDefault(item => string.Equals(item.GroupId, groupId, StringComparison.Ordinal));
        var subscribed = _document.Topics.Topics.Where(item => item.Groups.Contains(groupId, StringComparer.Ordinal)).ToList();
        var partitionCount = subscribed.Count == 0 ? 0 : subscribed.Min(item => item.Partitions);
        var plan = new ReplicaScalePlan
        {
            GroupId = groupId,
            CurrentMembers = group?.Members.Count ?? 0,
            DesiredMembers = replicas,
            PartitionCount = partitionCount,
            Ceiling = partitionCount,
            SecondApproverRequired = false
        };
        plan.Notes.Add("Replica changes are reversible. A replica above the partition count is assigned no partitions and sits idle.");
        plan.Notes.Add($"The ceiling for {groupId} is {partitionCount} (maxReplicas cannot exceed the partition count).");
        if (group is null)
            plan.Errors.Add("The consumer group was not found.");
        if (replicas == plan.CurrentMembers)
            plan.Errors.Add("The requested replica count matches the current member count.");
        if (replicas < 0)
            plan.Errors.Add("The replica count cannot be negative.");
        if (partitionCount > 0 && replicas > partitionCount)
            plan.Errors.Add($"The replica count {replicas} is above the partition ceiling {partitionCount}. Extra replicas sit idle and are refused.");
        plan.Accepted = plan.Errors.Count == 0;
        plan.Summary = plan.Accepted ? string.Join(" ", plan.Notes) : string.Join(" ", plan.Errors);
        return plan.Accepted ? Ok(plan) : Bad(plan);
    }

    public KafkaOpsCall<ChangeRequestRecord> CreateScale(string groupId, int replicas, string reason, string correlationId)
    {
        if (!_document.Infra.Enabled)
            return Fail<ChangeRequestRecord>(_document.Infra.Detail);
        if (string.IsNullOrWhiteSpace(reason))
            return Fail<ChangeRequestRecord>("A reason is required.");
        var plan = PlanScale(groupId, replicas);
        if (plan.Value is not { Accepted: true })
            return Fail<ChangeRequestRecord>(plan.Error ?? "The replica plan was refused.");
        return Store(new ChangeRequestRecord
        {
            Id = Guid.NewGuid(),
            Kind = "ScaleReplicas",
            GroupId = groupId,
            BeforeReplicas = plan.Value.CurrentMembers,
            DesiredReplicas = replicas,
            Reason = reason.Trim(),
            Requester = "anonymous",
            Approver = "anonymous",
            Status = "Approved",
            DryRunSummary = plan.Value.Summary,
            CorrelationId = correlationId,
            CreatedUtc = DateTimeOffset.UtcNow,
            ApprovedUtc = DateTimeOffset.UtcNow
        });
    }

    public KafkaOpsCall<BrokerMovePlan> PlanDecommission(int brokerId)
    {
        var plan = new BrokerMovePlan { BrokerId = brokerId, SecondApproverRequired = true };
        var known = _document.Cluster.Brokers.Any(item => item.Id == brokerId);
        if (!known)
            plan.Errors.Add($"Broker {brokerId} is not in the cluster.");
        var held = _document.Cluster.Placements.Where(item => item.Replicas.Contains(brokerId) || item.Leader == brokerId).ToList();
        plan.AlreadyEmpty = known && held.Count == 0 && plan.Errors.Count == 0;
        if (plan.AlreadyEmpty)
            plan.Notes.Add($"Broker {brokerId} has no replicas. It can be stopped once the cluster is green.");
        else
        {
            var failures = new List<(string Topic, string Reason)>();
            foreach (var placement in held)
            {
                var others = _document.Cluster.Brokers.Select(item => item.Id).Where(id => id != brokerId && !placement.Replicas.Contains(id)).ToList();
                if (others.Count == 0)
                {
                    failures.Add((placement.Topic, $"would drop the replication factor if broker {brokerId} left."));
                    continue;
                }

                var next = placement.Replicas.Where(id => id != brokerId).Append(others[0]).ToList();
                plan.Moves.Add(new ReplicaMove
                {
                    Topic = placement.Topic,
                    Partition = placement.Partition,
                    FromBroker = brokerId,
                    ToBroker = others[0],
                    Replicas = next
                });
            }

            foreach (var group in failures
                .GroupBy(item => (Topic: item.Topic ?? "", Reason: item.Reason))
                .OrderBy(item => item.Key.Topic.StartsWith('_'))
                .ThenBy(item => item.Key.Topic, StringComparer.Ordinal))
            {
                var topic = group.Key.Topic;
                var count = group.Count();
                var prefix = topic.StartsWith('_') ? "(internal) " : "";
                var noun = count == 1 ? "partition" : "partitions";
                plan.Errors.Add($"{prefix}{topic}: {count} {noun} {group.Key.Reason}");
            }

            plan.Notes.Add("Removing a broker moves every replica off it first. The broker is stopped only after it has no replicas, no leaders, and the cluster has no under-replicated or offline partitions.");
            plan.Notes.Add($"{plan.Moves.Count} partition(s) move off broker {brokerId}.");
        }

        plan.Accepted = plan.Errors.Count == 0;
        plan.Summary = plan.Accepted ? string.Join(" ", plan.Notes) : string.Join(" ", plan.Errors);
        return plan.Accepted ? Ok(plan) : Bad(plan);
    }

    public KafkaOpsCall<BrokerMovePlan> PlanRebalance(int brokerId)
    {
        var plan = new BrokerMovePlan { BrokerId = brokerId, SecondApproverRequired = true };
        if (_document.Cluster.Brokers.All(item => item.Id != brokerId))
            plan.Errors.Add($"Broker {brokerId} is not in the cluster yet.");
        plan.Notes.Add("Rebalance moves replicas onto the broker until it holds its share. It does not stop a broker.");
        var candidate = _document.Cluster.Placements.FirstOrDefault(item => !item.Replicas.Contains(brokerId) && item.Replicas.Count > 0);
        if (candidate is not null && plan.Errors.Count == 0)
        {
            var source = candidate.Replicas[0];
            plan.Moves.Add(new ReplicaMove
            {
                Topic = candidate.Topic,
                Partition = candidate.Partition,
                FromBroker = source,
                ToBroker = brokerId,
                Replicas = candidate.Replicas.Where(id => id != source).Append(brokerId).ToList()
            });
        }

        plan.Notes.Add(plan.Moves.Count == 0
            ? $"Broker {brokerId} already holds its share of replicas."
            : $"{plan.Moves.Count} partition(s) move onto broker {brokerId}.");
        plan.Accepted = plan.Errors.Count == 0;
        plan.Summary = plan.Accepted ? string.Join(" ", plan.Notes) : string.Join(" ", plan.Errors);
        return plan.Accepted ? Ok(plan) : Bad(plan);
    }

    public KafkaOpsCall<ChangeRequestRecord> CreateBroker(string kind, int brokerId, string reason, string correlationId)
    {
        if (!_document.Infra.Enabled)
            return Fail<ChangeRequestRecord>(string.IsNullOrWhiteSpace(_document.Infra.Detail) ? DisabledDetail : _document.Infra.Detail);
        if (string.IsNullOrWhiteSpace(reason))
            return Fail<ChangeRequestRecord>("A reason is required.");
        return Store(new ChangeRequestRecord
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            BrokerId = brokerId,
            Reason = reason.Trim(),
            Requester = "anonymous",
            Status = "Pending",
            SecondApproverRequired = true,
            CorrelationId = correlationId,
            CreatedUtc = DateTimeOffset.UtcNow,
            DryRunSummary = kind
        });
    }

    public KafkaOpsCall<KafkaBrowsePage> Messages(string topic, string mode, IReadOnlyList<int> partitions, long? offset, long? timestamp, int limit, string key, string headerName, string headerValue, int page = 1, string? text = null, string? header = null, string? valueContains = null, string? messageType = null, long? from = null, long? to = null)
    {
        var cleanMode = (mode ?? "").Trim().ToLowerInvariant();
        if (cleanMode.Length == 0)
            cleanMode = "newest";
        if (cleanMode is not ("newest" or "oldest" or "from-offset" or "since"))
            return Fail<KafkaBrowsePage>("Mode must be newest, oldest, from-offset, or since.");
        if (limit < 1 || limit > KafkaBrowseLimits.MaxLimit)
            return Fail<KafkaBrowsePage>("Limit must be from 1 to 50.");
        if (cleanMode == "from-offset" && offset is null)
            return Fail<KafkaBrowsePage>("From offset needs an offset.");
        if (cleanMode == "since" && timestamp is null)
            return Fail<KafkaBrowsePage>("Since time needs a timestamp.");

        var admission = KafkaBrowseAllowList.Admit(topic, null);
        if (!admission.Allowed)
            return Fail<KafkaBrowsePage>(admission.Reason);

        var result = SamplePage(admission.Topic, cleanMode);
        if (string.Equals(key, "cap", StringComparison.Ordinal))
        {
            result.Metadata.CapHit = true;
            result.Metadata.Truncated = true;
            if (result.Records.Count > 0)
                result.Records[0].Truncated = true;
        }
        else if (!string.IsNullOrEmpty(key))
        {
            result.Records = result.Records.Where(record => (record.Key ?? "").Contains(key, StringComparison.Ordinal)).ToList();
        }

        if (!string.IsNullOrEmpty(headerName) && !string.IsNullOrEmpty(headerValue))
        {
            result.Records = result.Records.Where(record => record.Headers.Any(header =>
                string.Equals(header.Name, headerName, StringComparison.OrdinalIgnoreCase)
                && header.Value.Contains(headerValue, StringComparison.Ordinal))).ToList();
        }

        if (partitions.Count > 0)
            result.Records = result.Records.Where(record => partitions.Contains(record.Partition)).ToList();
        var produced = ProducedFor(admission.Topic, partitions, key, headerName, headerValue);
        if (produced.Count > 0)
            result.Records.InsertRange(0, produced);
        result.Records = result.Records.Where(record => KafkaMessageWindow.Matches(record, text, key, header, valueContains, messageType, from, to)).ToList();
        if (cleanMode == "oldest")
            result.Records = result.Records.OrderBy(record => record.TimestampUnixMs).ThenBy(record => record.Offset).ToList();
        else
            result.Records = result.Records.OrderByDescending(record => record.TimestampUnixMs).ThenByDescending(record => record.Offset).ToList();
        result.Metadata.Total = result.Records.Count;
        var size = KafkaMessageWindow.Size(limit);
        var number = page < 1 ? 1 : page;
        result.Records = result.Records.Skip((number - 1) * size).Take(size).ToList();
        result.Metadata.Returned = result.Records.Count;
        return Ok(result);
    }

    public KafkaOpsCall<ChangeRequestRecord> ProduceMessage(string topic, string? headers, string? key, string? value, string? reason, string? confirmation, string correlationId)
    {
        var typed = (topic ?? "").Trim();
        if (!string.Equals((confirmation ?? "").Trim(), typed, StringComparison.Ordinal))
            return Fail<ChangeRequestRecord>(KafkaProduceRules.ConfirmSentence);
        if (string.IsNullOrWhiteSpace(reason))
            return Fail<ChangeRequestRecord>(KafkaProduceRules.ReasonSentence);
        if (reason.Trim().Length > KafkaProduceRules.MaxReason)
            return Fail<ChangeRequestRecord>(KafkaProduceRules.ReasonLengthSentence);

        var review = KafkaProduceRules.Evaluate(typed, headers, key, value);
        if (!review.Accepted)
            return Fail<ChangeRequestRecord>(review.Error);

        lock (_gate)
        {
            if (_document.Topics.ReadOnly)
                return Fail<ChangeRequestRecord>("Kafka changes are read-only in this environment.");

            if (!_produced.TryGetValue(review.Topic, out var list))
            {
                list = [];
                _produced[review.Topic] = list;
            }

            if (list.Count >= 50)
                list.RemoveAt(0);
            var offset = list.Count == 0 ? 1_000 : list.Max(item => item.Offset) + 1;
            list.Add(Record(
                review.Topic,
                0,
                offset,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                review.Key,
                review.Value,
                review.Headers,
                false));

            var now = DateTimeOffset.UtcNow;
            return Store(new ChangeRequestRecord
            {
                Id = Guid.NewGuid(),
                Kind = "Produce",
                Topic = review.Topic,
                Family = review.Topic,
                Reason = reason.Trim(),
                Requester = "anonymous",
                Status = "Done",
                DryRunSummary = review.Summary,
                Progress = "One message was produced.",
                CorrelationId = correlationId,
                CreatedUtc = now,
                ExecutedUtc = now,
                ConvergedUtc = now,
                ClosedUtc = now
            });
        }
    }

    public KafkaOpsCall<KafkaFamilyView> Family(string topic)
    {
        var admission = KafkaBrowseAllowList.Admit(topic, null);
        if (!admission.Allowed)
            return Fail<KafkaFamilyView>(admission.Reason);

        var known = new HashSet<string>(_document.Topics.Topics.Select(row => row.Topic), StringComparer.Ordinal);
        var members = KafkaBrowseAllowList.MembersOf(admission.Main).ToList();
        foreach (var member in members)
        {
            var onBroker = known.Contains(member.Topic) || (member.Kind == KafkaBrowseAllowList.KindError && known.Contains(member.Main));
            member.Exists = onBroker;
            member.Browsable = true;
            member.LagKnown = true;
            if (!onBroker)
                continue;
            member.Partitions = 3;
            member.HighWatermarkSum = member.Kind == KafkaBrowseAllowList.KindMain ? 120 : 9;
            member.Lag = member.Kind == KafkaBrowseAllowList.KindMain ? 12 : 1;
        }

        return Ok(new KafkaFamilyView { Main = admission.Main, Members = members });
    }

    private static KafkaBrowsePage SamplePage(string topic, string mode)
    {
        var facilityId = "11111111-1111-1111-1111-111111111111";
        var patientId = "pat-9";
        var reportId = "22222222-2222-2222-2222-222222222222";
        var correlationId = "33333333-3333-3333-3333-333333333333";
        var key = "{\"facilityId\":\"" + facilityId + "\",\"patientId\":\"" + patientId + "\"}";
        var value = "{\"reportId\":\"" + reportId + "\",\"patientId\":\"" + patientId + "\",\"resourceType\":\"Bundle\"}";
        var headers = new List<KafkaBrowseHeader>
        {
            new() { Name = "X-Correlation-Id", Value = correlationId }
        };
        var first = Record(topic, 0, 120, 1_710_000_000_000, key, value, headers, false);
        var failedValue = "{\"reportId\":\"" + reportId + "\",\"note\":\"empty bundle\"}";
        var failedHeaders = new List<KafkaBrowseHeader>
        {
            new() { Name = "X-Correlation-Id", Value = correlationId },
            new() { Name = "X-Exception-Message", Value = "The resource bundle was empty." },
            new() { Name = "X-Exception-Service", Value = "Normalization" }
        };
        var second = Record(topic, 1, 88, 1_709_999_000_000, key, failedValue, failedHeaders, false);
        return new KafkaBrowsePage
        {
            Topic = topic,
            Mode = mode,
            Records = [first, second],
            Metadata = new KafkaBrowseMetadata { Returned = 2, ElapsedMs = 42 }
        };
    }

    private static KafkaBrowseRecord Record(string topic, int partition, long offset, long timestamp, string key, string value, List<KafkaBrowseHeader> headers, bool truncated) =>
        new()
        {
            Partition = partition,
            Offset = offset,
            TimestampUnixMs = timestamp,
            Key = key,
            Value = value,
            ValueSummary = KafkaBrowseDecoder.Summary(value),
            ValuePretty = KafkaBrowseDecoder.Pretty(value),
            Truncated = truncated,
            ByteSize = System.Text.Encoding.UTF8.GetByteCount(value),
            Headers = headers,
            Link = KafkaBrowseDecoder.Decode(topic, key, value, headers)
        };

    public KafkaOpsCall<ChangeRequestRecord> Get(Guid id)
    {
        lock (_gate)
        {
            return _requests.TryGetValue(id, out var record)
                ? Ok(record)
                : Fail<ChangeRequestRecord>("The change request was not found.");
        }
    }

    public KafkaOpsCall<ChangeRequestRecord> Approve(Guid id)
    {
        lock (_gate)
        {
            if (!_requests.TryGetValue(id, out var record))
                return Fail<ChangeRequestRecord>("The change request was not found.");
            if (record.Status != "Pending")
                return Fail<ChangeRequestRecord>("Only a pending request can be approved.");
            if (string.Equals(record.Requester, "anonymous", StringComparison.OrdinalIgnoreCase))
                return Fail<ChangeRequestRecord>("The requester cannot approve their own request.");
            record.Status = "Approved";
            record.Approver = "anonymous";
            record.ApprovedUtc = DateTimeOffset.UtcNow;
            return Ok(record);
        }
    }

    public KafkaOpsCall<ChangeRequestRecord> Reject(Guid id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Fail<ChangeRequestRecord>("A rejection reason is required.");
        lock (_gate)
        {
            if (!_requests.TryGetValue(id, out var record))
                return Fail<ChangeRequestRecord>("The change request was not found.");
            record.Status = "Rejected";
            record.Failure = reason.Trim();
            record.ClosedUtc = DateTimeOffset.UtcNow;
            return Ok(record);
        }
    }

    public KafkaOpsCall<ChangeRequestRecord> Execute(Guid id)
    {
        lock (_gate)
        {
            if (!_requests.TryGetValue(id, out var record))
                return Fail<ChangeRequestRecord>("The change request was not found.");
            if (record.Status is not ("Approved" or "Pending"))
                return Fail<ChangeRequestRecord>("The request is not ready to run.");
            if (!_document.Infra.Enabled && record.Kind is "ScaleReplicas" or "AddBroker" or "DecommissionBroker" or "Rebalance")
                return Fail<ChangeRequestRecord>(_document.Infra.Detail);
            record.Status = "Converging";
            record.ExecutedUtc = DateTimeOffset.UtcNow;
            record.Progress = record.Kind switch
            {
                "ScaleReplicas" => "Waiting until the group is Stable and every partition is assigned.",
                "DecommissionBroker" => "Moving replicas. The broker is not stopped until it is empty and the cluster is green.",
                "AddBroker" => "Waiting until the broker is registered and the cluster has no offline partitions.",
                "Rebalance" => "Moving replicas onto the broker.",
                _ => "Waiting until every subscribed group has every partition assigned."
            };
            return Ok(record);
        }
    }

    public KafkaOpsCall<ChangeRequestRecord> Cancel(Guid id)
    {
        lock (_gate)
        {
            if (!_requests.TryGetValue(id, out var record))
                return Fail<ChangeRequestRecord>("The change request was not found.");
            if (record.Kind is not ("DecommissionBroker" or "Rebalance"))
                return Fail<ChangeRequestRecord>("Only an in-flight reassignment can be cancelled.");
            if (record.Status is not ("Executing" or "Converging" or "TimedOut"))
                return Fail<ChangeRequestRecord>("This request is not in flight.");
            return Store(new ChangeRequestRecord
            {
                Id = Guid.NewGuid(),
                Kind = "CancelReassignment",
                Topic = record.Topic,
                Family = record.Family,
                BrokerId = record.BrokerId,
                Requester = "anonymous",
                Status = "Pending",
                SecondApproverRequired = true,
                Reason = "Cancel the in-flight reassignment.",
                DryRunSummary = "Cancel the reassignment and check that replicas return to the original assignment.",
                CreatedUtc = DateTimeOffset.UtcNow
            });
        }
    }

    private List<KafkaBrowseRecord> ProducedFor(string topic, IReadOnlyList<int> partitions, string key, string headerName, string headerValue)
    {
        lock (_gate)
        {
            if (!_produced.TryGetValue(topic, out var list))
                return [];
            return list.Where(record => KeepProduced(record, partitions, key, headerName, headerValue)).ToList();
        }
    }

    private static bool KeepProduced(KafkaBrowseRecord record, IReadOnlyList<int> partitions, string key, string headerName, string headerValue)
    {
        if (!string.Equals(key, "cap", StringComparison.Ordinal) && !string.IsNullOrEmpty(key) && !(record.Key ?? "").Contains(key, StringComparison.Ordinal))
            return false;
        if (!string.IsNullOrEmpty(headerName) && !string.IsNullOrEmpty(headerValue))
        {
            var hit = record.Headers.Any(header =>
                string.Equals(header.Name, headerName, StringComparison.OrdinalIgnoreCase)
                && header.Value.Contains(headerValue, StringComparison.Ordinal));
            if (!hit)
                return false;
        }

        return partitions.Count == 0 || partitions.Contains(record.Partition);
    }

    private KafkaTopicRow? FindTopic(string topic) =>
        _document.Topics.Topics.FirstOrDefault(item => string.Equals(item.Topic, topic, StringComparison.Ordinal));

    private KafkaOpsCall<ChangeRequestRecord> Store(ChangeRequestRecord record)
    {
        lock (_gate)
            _requests[record.Id] = record;
        return Ok(record);
    }

    private static KafkaOpsCall<T> Ok<T>(T value) => new() { Status = 200, Value = value };

    private static KafkaOpsCall<T> Bad<T>(T value) => new()
    {
        Status = 400,
        Value = value,
        Error = value switch
        {
            PartitionPlan plan => plan.Summary,
            ReplicaScalePlan plan => plan.Summary,
            BrokerMovePlan plan => plan.Summary,
            ReplicationFactorPlan plan => plan.Summary,
            _ => "The dry run was refused."
        }
    };

    private static KafkaOpsCall<T> Fail<T>(string error) => new() { Status = 400, Error = error };

    private static List<GroupProgressRow> AffectedGroupsCopied(List<string> groups) =>
        groups.Select(group => new GroupProgressRow { GroupId = group, ExpectedPartitions = 0, Complete = false }).ToList();

    private sealed class FixtureDocument
    {
        public KafkaTopicsResponse Topics { get; set; } = new();
        public KafkaGroupsResponse Groups { get; set; } = new();
        public KafkaCapabilitiesResponse Capabilities { get; set; } = new();
        public ClusterSnapshot Cluster { get; set; } = new();
        public InfraStatus Infra { get; set; } = new() { Provider = "Disabled", Detail = DisabledDetail };
        public List<ChangeRequestRecord> Requests { get; set; } = [];
    }
}
