using Confluent.Kafka;
using Confluent.Kafka.Admin;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public sealed class MigrationTopicFacts
{
    public bool Present { get; set; }
    public string TopicId { get; set; } = "";
    public int Partitions { get; set; }
    public int ReplicationFactor { get; set; }
    public bool FullIsr { get; set; } = true;
    public bool LeadersSkewed { get; set; }
    public List<long> HighWatermarks { get; set; } = [];
    public List<long> LogStarts { get; set; } = [];
    public Dictionary<string, string> Configs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string EffectiveCleanupPolicy { get; set; } = "";
    public int? EffectiveMinInSyncReplicas { get; set; }
    public List<MigrationPartitionFacts> PartitionRows { get; set; } = [];
    public List<string> AuthorizedOperations { get; set; } = [];
    public long OldestCreateTimeMs { get; set; }
}

public sealed class MigrationPartitionFacts
{
    public int Partition { get; set; }
    public int Leader { get; set; } = -1;
    public List<int> Replicas { get; set; } = [];
    public List<int> Isr { get; set; } = [];
    public bool PreferredLeader { get; set; }
    public long LogStart { get; set; }
    public long HighWatermark { get; set; }
}

public sealed class MigrationGroupFacts
{
    public string GroupId { get; set; } = "";
    public string State { get; set; } = "";
    public int Members { get; set; }
    public List<long> Committed { get; set; } = [];
    public List<string> AuthorizedOperations { get; set; } = [];
}

public sealed class TopicConfigRow
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string Source { get; set; } = "";
}

public interface IKafkaMigrationAdmin
{
    bool DedicatedConnection { get; }
    Task<bool> ClusterHealthyAsync(CancellationToken cancellationToken);
    Task<MigrationTopicFacts?> DescribeAsync(string topic, CancellationToken cancellationToken);
    Task<IReadOnlyList<TopicConfigRow>> DescribeConfigsAsync(string topic, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<string, string>> DescribeBrokerConfigAsync(int brokerId, CancellationToken cancellationToken);
    Task<string> CreateTopicAsync(string name, int partitions, int replicationFactor, IReadOnlyDictionary<string, string> configs, bool validateOnly, CancellationToken cancellationToken);
    Task DeleteTopicAsync(string name, CancellationToken cancellationToken);
    Task GrowPartitionsAsync(string name, int to, CancellationToken cancellationToken);
    Task SetRetentionAsync(string topic, long retentionMs, CancellationToken cancellationToken);
    Task WriteOffsetsAsync(string group, string topic, IReadOnlyList<long> offsets, CancellationToken cancellationToken);
    Task<MigrationGroupFacts?> DescribeGroupAsync(string group, string topic, int partitions, CancellationToken cancellationToken);
    Task ElectLeadersAsync(string topic, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListGroupsWhenHealthyAsync(CancellationToken cancellationToken);
    Task<CopyOutcome> CopyBackupAsync(MigrationRecord record, CancellationToken cancellationToken);
    Task<CopyOutcome> InspectBackupAsync(MigrationRecord record, CancellationToken cancellationToken);
    Task AppendJournalAsync(MigrationRecord record, CancellationToken cancellationToken);
    Task<IReadOnlyList<MigrationRecord>> ReadJournalAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListLinkMigTopicsAsync(CancellationToken cancellationToken);
    Task EnsureJournalAsync(CancellationToken cancellationToken);
}

public sealed class KafkaMigrationAdmin : IKafkaMigrationAdmin, IDisposable
{
    private readonly KafkaConnection _shared;
    private readonly KafkaOpsOptions _options;
    private readonly KafkaOpsConnectionOptions _dedicated;

    public KafkaMigrationAdmin(KafkaConnection shared, KafkaOpsOptions options, KafkaOpsConnectionOptions dedicated)
    {
        _shared = shared;
        _options = options;
        _dedicated = dedicated;
    }

    public bool DedicatedConnection =>
        !string.IsNullOrWhiteSpace(_dedicated.BootstrapServers);

    public async Task<bool> ClusterHealthyAsync(CancellationToken cancellationToken)
    {
        using var admin = NewAdmin();
        Metadata meta;
        try
        {
            meta = admin.GetMetadata(TimeSpan.FromSeconds(10));
        }
        catch (KafkaException)
        {
            return false;
        }

        if (meta.Brokers is null || meta.Brokers.Count == 0)
            return false;
        foreach (var broker in meta.Brokers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(broker.Host))
                return false;
            try
            {
                await admin.DescribeConfigsAsync(
                    [new ConfigResource { Type = ResourceType.Broker, Name = broker.BrokerId.ToString() }],
                    new DescribeConfigsOptions { RequestTimeout = TimeSpan.FromSeconds(5) });
            }
            catch (KafkaException)
            {
                return false;
            }
        }

        return true;
    }

    public async Task<MigrationTopicFacts?> DescribeAsync(string topic, CancellationToken cancellationToken)
    {
        using var admin = NewAdmin();
        DescribeTopicsResult described;
        try
        {
            described = await admin.DescribeTopicsAsync(
                TopicCollection.OfTopicNames([topic]),
                new DescribeTopicsOptions { IncludeAuthorizedOperations = true, RequestTimeout = TimeSpan.FromSeconds(20) });
        }
        catch (DescribeTopicsException ex) when (TopicMissing(ex))
        {
            return null;
        }
        catch (KafkaException ex) when (ex.Error.Code is ErrorCode.UnknownTopicOrPart)
        {
            return null;
        }

        var item = described.TopicDescriptions.FirstOrDefault();
        if (item is null || item.Error.IsError)
            return null;
        var facts = new MigrationTopicFacts
        {
            Present = true,
            TopicId = IdText(item.TopicId),
            Partitions = item.Partitions?.Count ?? 0,
            ReplicationFactor = item.Partitions?.FirstOrDefault()?.Replicas?.Count ?? 0,
            AuthorizedOperations = item.AuthorizedOperations?.Select(operation => operation.ToString()).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? []
        };
        var leaders = new List<int>();
        foreach (var partition in item.Partitions ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            var replicas = partition.Replicas?.Select(node => node.Id).ToList() ?? [];
            var isr = partition.ISR?.Select(node => node.Id).ToList() ?? [];
            var leader = partition.Leader?.Id ?? -1;
            leaders.Add(leader);
            var preferred = replicas.Count > 0 && leader == replicas[0];
            if (isr.Count != replicas.Count || replicas.Count == 0)
                facts.FullIsr = false;
            facts.PartitionRows.Add(new MigrationPartitionFacts
            {
                Partition = partition.Partition,
                Leader = leader,
                Replicas = replicas,
                Isr = isr,
                PreferredLeader = preferred
            });
        }

        if (leaders.Count > 0)
        {
            var distinct = leaders.Where(id => id >= 0).Distinct().Count();
            var ideal = distinct == 0 ? 1d : 1d / distinct;
            var max = leaders.GroupBy(id => id).Max(group => group.Count() / (double)leaders.Count);
            facts.LeadersSkewed = max - ideal > 0.34;
        }

        var configs = await DescribeConfigsAsync(topic, cancellationToken);
        ApplyDescribedConfigs(facts, configs);
        await WatermarksAsync(facts, topic, cancellationToken);
        return facts;
    }

    /// <summary>
    /// Create overrides stay limited to DynamicTopicConfig. cleanup.policy and
    /// min.insync.replicas are read from the effective row, including broker defaults.
    /// </summary>
    internal static void ApplyDescribedConfigs(MigrationTopicFacts facts, IEnumerable<TopicConfigRow> rows)
    {
        foreach (var row in rows)
        {
            if (string.Equals(row.Name, "cleanup.policy", StringComparison.OrdinalIgnoreCase))
                facts.EffectiveCleanupPolicy = row.Value ?? "";
            if (string.Equals(row.Name, "min.insync.replicas", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(row.Value, out var minInSync))
                facts.EffectiveMinInSyncReplicas = minInSync;
            if (!string.Equals(row.Source, "DynamicTopicConfig", StringComparison.Ordinal))
                continue;
            facts.Configs[row.Name] = row.Value;
        }
    }

    public async Task<IReadOnlyList<TopicConfigRow>> DescribeConfigsAsync(string topic, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var admin = NewAdmin();
        var described = await admin.DescribeConfigsAsync(
            [new ConfigResource { Type = ResourceType.Topic, Name = topic }],
            new DescribeConfigsOptions { RequestTimeout = TimeSpan.FromSeconds(15) });
        var rows = new List<TopicConfigRow>();
        foreach (var result in described)
        {
            foreach (var entry in result.Entries)
            {
                rows.Add(new TopicConfigRow
                {
                    Name = entry.Key,
                    Value = entry.Value.Value ?? "",
                    Source = entry.Value.Source.ToString()
                });
            }
        }

        return rows.OrderBy(row => row.Name, StringComparer.Ordinal).ToList();
    }

    public async Task<IReadOnlyDictionary<string, string>> DescribeBrokerConfigAsync(int brokerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var admin = NewAdmin();
        var described = await admin.DescribeConfigsAsync(
            [new ConfigResource { Type = ResourceType.Broker, Name = brokerId.ToString() }],
            new DescribeConfigsOptions { RequestTimeout = TimeSpan.FromSeconds(15) });
        var configs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in described)
        {
            foreach (var entry in result.Entries)
                configs[entry.Key] = entry.Value.Value ?? "";
        }

        return configs;
    }

    public async Task<string> CreateTopicAsync(string name, int partitions, int replicationFactor, IReadOnlyDictionary<string, string> configs, bool validateOnly, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var admin = NewAdmin();
        await admin.CreateTopicsAsync(
            [new TopicSpecification
            {
                Name = name,
                NumPartitions = partitions,
                ReplicationFactor = (short)Math.Clamp(replicationFactor, 1, 10),
                Configs = new Dictionary<string, string>(configs, StringComparer.Ordinal)
            }],
            new CreateTopicsOptions { ValidateOnly = validateOnly, RequestTimeout = TimeSpan.FromSeconds(30) });
        if (validateOnly)
            return "";
        var described = await DescribeAsync(name, cancellationToken);
        return described?.TopicId ?? "";
    }

    public async Task DeleteTopicAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!name.StartsWith("_linkmig-", StringComparison.Ordinal) && name.Contains('\n'))
            throw new KafkaOpsRejectedException("The topic name is invalid.");
        using var admin = NewAdmin();
        await admin.DeleteTopicsAsync([name], new DeleteTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(30) });
    }

    public async Task GrowPartitionsAsync(string name, int to, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var admin = NewAdmin();
        await admin.CreatePartitionsAsync(
            [new PartitionsSpecification { Topic = name, IncreaseTo = to }],
            new CreatePartitionsOptions { RequestTimeout = TimeSpan.FromSeconds(30) });
    }

    public async Task SetRetentionAsync(string topic, long retentionMs, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var admin = NewAdmin();
        await admin.IncrementalAlterConfigsAsync(
            new Dictionary<ConfigResource, List<ConfigEntry>>
            {
                [new ConfigResource { Type = ResourceType.Topic, Name = topic }] =
                [
                    new ConfigEntry { Name = "retention.ms", Value = retentionMs.ToString(), IncrementalOperation = AlterConfigOpType.Set }
                ]
            },
            new IncrementalAlterConfigsOptions { RequestTimeout = TimeSpan.FromSeconds(20) });
    }

    public async Task WriteOffsetsAsync(string group, string topic, IReadOnlyList<long> offsets, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pairs = new List<TopicPartitionOffset>();
        for (var i = 0; i < offsets.Count; i++)
            pairs.Add(new TopicPartitionOffset(topic, i, new Offset(offsets[i])));
        using var admin = NewAdmin();
        await admin.AlterConsumerGroupOffsetsAsync(
            [new ConsumerGroupTopicPartitionOffsets(group, pairs)],
            new AlterConsumerGroupOffsetsOptions { RequestTimeout = TimeSpan.FromSeconds(20) });
    }

    public async Task<MigrationGroupFacts?> DescribeGroupAsync(string group, string topic, int partitions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var admin = NewAdmin();
        var described = await admin.DescribeConsumerGroupsAsync(
            [group],
            new DescribeConsumerGroupsOptions { IncludeAuthorizedOperations = true, RequestTimeout = TimeSpan.FromSeconds(15) });
        var item = described.ConsumerGroupDescriptions.FirstOrDefault();
        if (item is null || item.Error.Code is ErrorCode.GroupIdNotFound || item.State == ConsumerGroupState.Dead)
            return new MigrationGroupFacts { GroupId = group, State = "Empty" };
        if (item.Error.IsError)
            return null;
        var facts = new MigrationGroupFacts
        {
            GroupId = group,
            State = item.State.ToString(),
            Members = item.Members?.Count ?? 0,
            AuthorizedOperations = item.AuthorizedOperations?.Select(operation => operation.ToString()).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToList() ?? []
        };
        var wanted = Enumerable.Range(0, Math.Max(0, partitions)).Select(partition => new TopicPartition(topic, partition)).ToList();
        if (wanted.Count == 0)
            return facts;
        var listed = await admin.ListConsumerGroupOffsetsAsync(
            [new ConsumerGroupTopicPartitions(group, wanted)],
            new ListConsumerGroupOffsetsOptions { RequestTimeout = TimeSpan.FromSeconds(15) });
        var committed = Enumerable.Repeat(-1L, wanted.Count).ToList();
        foreach (var result in listed)
        {
            foreach (var pair in result.Partitions ?? [])
            {
                if (pair.Topic == topic && pair.Partition.Value >= 0 && pair.Partition.Value < committed.Count && !pair.Offset.IsSpecial)
                    committed[pair.Partition.Value] = pair.Offset.Value;
            }
        }

        facts.Committed = committed;
        return facts;
    }

    public async Task ElectLeadersAsync(string topic, CancellationToken cancellationToken)
    {
        var described = await DescribeAsync(topic, cancellationToken);
        if (described is null)
            return;
        var partitions = described.PartitionRows.Select(row => new TopicPartition(topic, row.Partition)).ToList();
        using var admin = NewAdmin();
        await admin.ElectLeadersAsync(KafkaLeaderElection.Kind, partitions, new ElectLeadersOptions { RequestTimeout = TimeSpan.FromSeconds(20) });
    }

    public async Task<IReadOnlyList<string>> ListGroupsWhenHealthyAsync(CancellationToken cancellationToken)
    {
        if (!await ClusterHealthyAsync(cancellationToken))
            throw new KafkaOpsRejectedException("Group discovery runs only when every broker is up.");
        return await ListGroupsOnShortLivedClientAsync(cancellationToken);
    }

    public Task<CopyOutcome> InspectBackupAsync(MigrationRecord record, CancellationToken cancellationToken) =>
        PlanBackupAsync(record, produce: false, cancellationToken);

    public Task<CopyOutcome> CopyBackupAsync(MigrationRecord record, CancellationToken cancellationToken) =>
        PlanBackupAsync(record, produce: true, cancellationToken);

    private async Task<CopyOutcome> PlanBackupAsync(MigrationRecord record, bool produce, CancellationToken cancellationToken)
    {
        var sourceFacts = await DescribeAsync(record.Topic, cancellationToken);
        var destinationFacts = await DescribeAsync(record.BackupTopic, cancellationToken);
        if (sourceFacts is null || destinationFacts is null)
            return new CopyOutcome { Failure = "The backup copy could not read the source or the temp topic." };
        var source = await ReadLogAsync(record.Topic, sourceFacts, record.FrozenHighWatermarks, cancellationToken);
        var destination = await ReadLogAsync(record.BackupTopic, destinationFacts, [], cancellationToken);
        var outcome = LogCopier.Copy(new CopyRequest
        {
            MigrationId = record.Id,
            SourceTopic = record.Topic,
            TargetPartitions = record.TargetPartitions,
            RequireComputedTargetEqualsPartition = false,
            Source = source.Records,
            Destination = destination.Records
        });
        if (!outcome.Ok)
            return outcome;
        if (produce && outcome.Written.Count > 0)
        {
            await ProduceAsync(record.BackupTopic, outcome.Written, sourceFacts.Configs, cancellationToken);
            return outcome;
        }

        var complete = source.ReachedEnd && destination.ReachedEnd;
        var match = outcome.Written.Count == 0 && complete && LogCopier.Matches(source.Records, destination.Records);
        return new CopyOutcome
        {
            Ok = true,
            Written = outcome.Written,
            Counts = outcome.Counts,
            Digests = outcome.Digests,
            Total = outcome.Total,
            ReachedEnd = complete,
            CountsMatch = match,
            DigestsMatch = match
        };
    }

    public async Task AppendJournalAsync(MigrationRecord record, CancellationToken cancellationToken)
    {
        await EnsureJournalAsync(cancellationToken);
        using var producer = NewProducer(1_048_576);
        var message = new Message<byte[]?, byte[]?>
        {
            Key = System.Text.Encoding.UTF8.GetBytes(record.Id.ToString("N")),
            Value = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(record)
        };
        await producer.ProduceAsync(KafkaTopicCatalog.JournalTopicName, message, cancellationToken);
        producer.Flush(TimeSpan.FromSeconds(10));
    }

    public async Task<IReadOnlyList<MigrationRecord>> ReadJournalAsync(CancellationToken cancellationToken)
    {
        await EnsureJournalAsync(cancellationToken);
        var records = new Dictionary<Guid, MigrationRecord>();
        using var consumer = NewReader();
        // Seek immediately after Assign fails on this client. The offset belongs on the assignment.
        consumer.Assign(new TopicPartitionOffset(KafkaTopicCatalog.JournalTopicName, 0, Offset.Beginning));
        var idle = 0;
        while (idle < 2)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = consumer.Consume(TimeSpan.FromSeconds(1));
            if (result is null)
            {
                idle++;
                continue;
            }

            idle = 0;
            try
            {
                var record = System.Text.Json.JsonSerializer.Deserialize<MigrationRecord>(result.Message.Value);
                if (record is not null && (!records.TryGetValue(record.Id, out var prior) || record.StepSequence >= prior.StepSequence))
                    records[record.Id] = record;
            }
            catch (System.Text.Json.JsonException)
            {
                // A corrupt journal record is skipped. The store copy remains.
            }
        }

        return records.Values.ToList();
    }

    public Task<IReadOnlyList<string>> ListLinkMigTopicsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var admin = NewAdmin();
        var meta = admin.GetMetadata(TimeSpan.FromSeconds(15));
        IReadOnlyList<string> names = meta.Topics
            .Select(topic => topic.Topic)
            .Where(name => name.StartsWith("_linkmig-", StringComparison.Ordinal))
            .ToList();
        return Task.FromResult(names);
    }

    public async Task EnsureJournalAsync(CancellationToken cancellationToken)
    {
        var existing = await DescribeAsync(KafkaTopicCatalog.JournalTopicName, cancellationToken);
        if (existing is not null)
            return;
        using var admin = NewAdmin();
        var brokers = admin.GetMetadata(TimeSpan.FromSeconds(10)).Brokers?.Count ?? 1;
        var rf = (short)Math.Min(3, Math.Max(1, brokers));
        var insync = Math.Min(2, (int)rf);
        try
        {
            await admin.CreateTopicsAsync(
                [new TopicSpecification
                {
                    Name = KafkaTopicCatalog.JournalTopicName,
                    NumPartitions = 1,
                    ReplicationFactor = rf,
                    Configs = new Dictionary<string, string> { ["min.insync.replicas"] = insync.ToString(), ["cleanup.policy"] = "delete" }
                }],
                new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(20) });
        }
        catch (KafkaException ex) when (ex.Error.Code is ErrorCode.TopicAlreadyExists)
        {
        }
    }

    private async Task<IReadOnlyList<string>> ListGroupsOnShortLivedClientAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var admin = NewAdmin();
        var listed = await admin.ListConsumerGroupsAsync(new ListConsumerGroupsOptions { RequestTimeout = TimeSpan.FromSeconds(15) });
        return listed.Valid.Select(group => group.GroupId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
    }

    private async Task WatermarksAsync(MigrationTopicFacts facts, string topic, CancellationToken cancellationToken)
    {
        if (facts.Partitions == 0)
            return;
        using var consumer = NewReader();
        long oldest = 0;
        foreach (var row in facts.PartitionRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var marks = consumer.QueryWatermarkOffsets(new TopicPartition(topic, row.Partition), TimeSpan.FromSeconds(10));
            row.LogStart = marks.Low.IsSpecial ? 0 : marks.Low.Value;
            row.HighWatermark = marks.High.IsSpecial ? 0 : marks.High.Value;
            facts.LogStarts.Add(row.LogStart);
            facts.HighWatermarks.Add(row.HighWatermark);
        }

        if (facts.HighWatermarks.Any(mark => mark > 0))
            oldest = await OldestTimestampAsync(consumer, topic, facts, cancellationToken);
        facts.OldestCreateTimeMs = oldest;
    }

    private static async Task<long> OldestTimestampAsync(IConsumer<byte[]?, byte[]?> consumer, string topic, MigrationTopicFacts facts, CancellationToken cancellationToken)
    {
        var oldest = long.MaxValue;
        var assignments = facts.PartitionRows
            .Where(row => row.HighWatermark > row.LogStart)
            .Select(row => new TopicPartitionOffset(topic, row.Partition, new Offset(row.LogStart)))
            .ToList();
        if (assignments.Count == 0)
            return 0;
        consumer.Assign(assignments);
        var remaining = assignments.Count;
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (remaining > 0 && DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = consumer.Consume(TimeSpan.FromMilliseconds(200));
            if (result is null)
                continue;
            remaining--;
            if (result.Message?.Timestamp.UnixTimestampMs is long stamp && stamp > 0 && stamp < oldest)
                oldest = stamp;
        }

        return oldest == long.MaxValue ? 0 : oldest;
    }

    private async Task<LogRead> ReadLogAsync(string topic, MigrationTopicFacts facts, IReadOnlyList<long> frozenEnd, CancellationToken cancellationToken)
    {
        var read = new LogRead();
        using var consumer = NewReader();
        var assignments = new List<TopicPartitionOffset>();
        var ends = new Dictionary<int, long>();
        for (var i = 0; i < facts.PartitionRows.Count; i++)
        {
            var row = facts.PartitionRows[i];
            var end = i < frozenEnd.Count ? frozenEnd[i] : row.HighWatermark;
            ends[row.Partition] = end;
            if (end <= row.LogStart)
                continue;
            assignments.Add(new TopicPartitionOffset(topic, row.Partition, new Offset(row.LogStart)));
        }

        if (assignments.Count == 0)
        {
            read.ReachedEnd = true;
            return read;
        }

        var pending = new HashSet<int>(assignments.Select(assignment => assignment.Partition.Value));
        consumer.Assign(assignments);
        var idle = 0;
        while (idle < 3 && pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = consumer.Consume(TimeSpan.FromMilliseconds(400));
            if (result is null)
            {
                idle++;
                continue;
            }

            idle = 0;
            var end = ends.GetValueOrDefault(result.Partition.Value);
            if (result.Offset + 1 >= end)
                pending.Remove(result.Partition.Value);
            if (result.Offset >= end)
                continue;
            read.Records.Add(new CopiedRecord
            {
                Key = result.Message.Key,
                Value = result.Message.Value,
                TimestampMs = result.Message.Timestamp.UnixTimestampMs,
                Partition = result.Partition.Value,
                Offset = result.Offset.Value,
                Headers = result.Message.Headers?.Select(header => new CopiedHeader { Name = header.Key, Value = header.GetValueBytes() }).ToList() ?? []
            });
        }

        // Idle before the frozen end is an incomplete read. The caller retries. It does not treat the short log as verified.
        read.ReachedEnd = pending.Count == 0;
        return read;
    }

    private sealed class LogRead
    {
        public List<CopiedRecord> Records { get; } = [];
        public bool ReachedEnd { get; set; }
    }

    private async Task ProduceAsync(string topic, IReadOnlyList<CopiedRecord> written, IReadOnlyDictionary<string, string> configs, CancellationToken cancellationToken)
    {
        var max = 1_048_576;
        if (configs.TryGetValue("max.message.bytes", out var text) && int.TryParse(text, out var parsed))
            max = parsed;
        using var producer = NewProducer(max);
        var groups = written.GroupBy(record => record.Partition).ToList();
        await Task.WhenAll(groups.Select(group => ProducePartitionAsync(producer, topic, group.OrderBy(record => record.Offset).ToList(), cancellationToken)));
    }

    private static async Task ProducePartitionAsync(IProducer<byte[]?, byte[]?> producer, string topic, IReadOnlyList<CopiedRecord> records, CancellationToken cancellationToken)
    {
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var headers = new Headers();
            foreach (var header in record.Headers)
                headers.Add(header.Name, header.Value);
            var message = new Message<byte[]?, byte[]?>
            {
                Key = record.Key is { Length: > 0 } ? record.Key : null,
                Value = record.Value,
                Headers = headers,
                Timestamp = new Timestamp(record.TimestampMs, TimestampType.CreateTime)
            };
            await producer.ProduceAsync(new TopicPartition(topic, record.Partition), message, cancellationToken);
        }
    }

    private IAdminClient NewAdmin() => new AdminClientBuilder(AdminConfig()).Build();

    private IConsumer<byte[]?, byte[]?> NewReader()
    {
        var config = ConsumerConfig();
        config.EnableAutoCommit = false;
        config.EnableAutoOffsetStore = false;
        config.AutoOffsetReset = AutoOffsetReset.Earliest;
        config.IsolationLevel = IsolationLevel.ReadCommitted;
        config.AllowAutoCreateTopics = false;
        return new ConsumerBuilder<byte[]?, byte[]?>(config).Build();
    }

    private IProducer<byte[]?, byte[]?> NewProducer(int messageMaxBytes)
    {
        var config = ProducerConfig();
        config.Acks = Acks.All;
        config.EnableIdempotence = true;
        config.MaxInFlight = 5;
        config.CompressionType = CompressionType.Zstd;
        config.MessageMaxBytes = messageMaxBytes;
        config.AllowAutoCreateTopics = false;
        // Explicit TopicPartition copies do not consult the partitioner. ApplyProducer still
        // pins Murmur2Random so this producer matches every other .NET producer.
        KafkaClientDefaults.ApplyProducer(config);
        return new ProducerBuilder<byte[]?, byte[]?>(config).Build();
    }

    private AdminClientConfig AdminConfig()
    {
        var config = new AdminClientConfig { BootstrapServers = Servers(), ClientId = "LinkAdminBFF-migration" };
        ApplySasl(config);
        return config;
    }

    private ConsumerConfig ConsumerConfig()
    {
        // librdkafka rejects a consumer that has no group.id, including an assign-only reader.
        // This group never commits. Discovery ignores a group with no commits on the topic.
        var config = new ConsumerConfig
        {
            BootstrapServers = Servers(),
            ClientId = "LinkAdminBFF-migration-read",
            GroupId = "link-kafka-ops-migration-reader"
        };
        ApplySasl(config);
        return config;
    }

    private static bool TopicMissing(DescribeTopicsException ex)
    {
        var topics = ex.Results?.TopicDescriptions;
        return topics is { Count: > 0 } && topics.All(item => item.Error.Code == ErrorCode.UnknownTopicOrPart);
    }

    private ProducerConfig ProducerConfig()
    {
        var config = new ProducerConfig { BootstrapServers = Servers(), ClientId = "LinkAdminBFF-migration-write" };
        ApplySasl(config);
        return config;
    }

    private string Servers()
    {
        if (DedicatedConnection)
            return _dedicated.BootstrapServers.Trim();
        if (!string.Equals(_options.InfraProvider, "LocalCompose", StringComparison.OrdinalIgnoreCase))
            throw new KafkaOpsRejectedException("A dedicated KafkaOps connection is required outside LocalCompose.");
        return string.Join(",", _shared.BootstrapServers ?? []);
    }

    private void ApplySasl(ClientConfig config)
    {
        var sasl = DedicatedConnection ? _dedicated.SaslProtocolEnabled : _shared.SaslProtocolEnabled;
        if (!sasl)
            return;
        config.SecurityProtocol = DedicatedConnection ? SecurityProtocol.SaslPlaintext : _shared.Protocol;
        config.SaslMechanism = DedicatedConnection ? SaslMechanism.Plain : _shared.Mechanism;
        config.SaslUsername = DedicatedConnection ? _dedicated.SaslUsername : _shared.SaslUsername;
        config.SaslPassword = DedicatedConnection ? _dedicated.SaslPassword : _shared.SaslPassword;
    }

    private static string IdText(object? topicId)
    {
        var text = topicId?.ToString() ?? "";
        return text is "" or "00000000-0000-0000-0000-000000000000" ? "" : text;
    }

    public void Dispose()
    {
    }
}
