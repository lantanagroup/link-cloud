using System.Security.Claims;
using System.Text.Json;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Link.Authorization.Permissions;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public interface IMigrationRuntime
{
    bool CanMigrate(ClaimsPrincipal user);
    bool CanView(ClaimsPrincipal user);
    bool HasOrphanBlock { get; }
    Task<MigrationDryRun> PlanAsync(string topic, int partitions, bool backupSkip, bool backupSkipAcknowledged, IReadOnlyList<string> acknowledgedGroups, CancellationToken cancellationToken, Guid? exceptId = null);
    Task<MigrationRecord> RequestAsync(ClaimsPrincipal user, MigrationRequestBody body, CancellationToken cancellationToken);
    Task<MigrationRecord> ApproveAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken);
    Task<MigrationRecord> RejectAsync(ClaimsPrincipal user, Guid id, string reason, CancellationToken cancellationToken);
    Task<MigrationRecord> ExecuteAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken);
    Task<MigrationRecord> CommandAsync(ClaimsPrincipal user, Guid id, MigrationCommand command, string? typedName, CancellationToken cancellationToken);
    Task<MigrationRecord> ManualStepAsync(ClaimsPrincipal user, Guid id, string workload, CancellationToken cancellationToken);
    Task<MigrationRecord> GetAsync(ClaimsPrincipal user, Guid id, bool mutate, CancellationToken cancellationToken);
    Task<string> RunbookAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken);
    Task DeleteBackupAsync(ClaimsPrincipal user, string name, string typedName, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<string>> HoldsAsync(CancellationToken cancellationToken);
    Task<TopicDetailModel> DetailAsync(string topic, CancellationToken cancellationToken);
    Task<TopicConfigDiffModel> ConfigsAsync(string topic, string? diff, CancellationToken cancellationToken);
    Task TickOpenAsync(long fence, CancellationToken cancellationToken);
    Task ReconcileAsync(CancellationToken cancellationToken);
}

public sealed class MigrationRequestBody
{
    public string Topic { get; set; } = "";
    public int Partitions { get; set; }
    public string Reason { get; set; } = "";
    public string Confirmation { get; set; } = "";
    public bool BackupSkip { get; set; }
    public bool BackupSkipAcknowledged { get; set; }
    public List<string> AcknowledgedGroups { get; set; } = [];
    public string PlanHash { get; set; } = "";
}

public sealed class TopicDetailModel
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
    public Guid? OpenMigration { get; set; }
    public int TopicsFilePartitions { get; set; } = 3;
    public bool PartitionDrift { get; set; }
    public long EstimatedRecords { get; set; }
    public List<MigrationPartitionFacts> PartitionsDetail { get; set; } = [];
    public List<MigrationGroupFacts> Groups { get; set; } = [];
}

public sealed class TopicConfigDiffModel
{
    public string Topic { get; set; } = "";
    public string Diff { get; set; } = "";
    public List<TopicConfigRow> Configs { get; set; } = [];
    public List<string> Changes { get; set; } = [];
}

public sealed class MigrationRuntime : IMigrationRuntime
{
    private readonly bool _anonymous;
    private readonly IHostEnvironment _environment;
    private readonly KafkaOpsOptions _options;
    private readonly IMigrationStore _store;
    private readonly IKafkaOpsLease _lease;
    private readonly IKafkaMigrationAdmin _admin;
    private readonly IKafkaWorkloadControl _workloads;
    private readonly IKafkaInfraProvider _infra;
    private readonly IMigrationHoldRegistry _holds;
    private readonly ICacheService _cache;
    private readonly ILogger<MigrationRuntime> _logger;
    private readonly bool _readOnly;
    private readonly Dictionary<Guid, string> _copyFailures = [];
    private int _reconciled;

    public MigrationRuntime(
        IConfiguration configuration,
        IHostEnvironment environment,
        IOptions<KafkaOpsOptions> options,
        IMigrationStore store,
        IKafkaOpsLease lease,
        IKafkaMigrationAdmin admin,
        IKafkaWorkloadControl workloads,
        IKafkaInfraProvider infra,
        IMigrationHoldRegistry holds,
        ICacheService cache,
        ILogger<MigrationRuntime> logger)
    {
        _anonymous = configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");
        _environment = environment;
        _options = options.Value;
        _store = store;
        _lease = lease;
        _admin = admin;
        _workloads = workloads;
        _infra = infra;
        _holds = holds;
        var readOnlySetting = configuration.GetSection(KafkaOpsOptions.SectionName)["ReadOnly"];
        _readOnly = readOnlySetting is null ? environment.IsProduction() : options.Value.ReadOnly;
        _cache = cache;
        _logger = logger;
    }

    public bool HasOrphanBlock { get; private set; }

    public bool CanMigrate(ClaimsPrincipal user) =>
        _anonymous || KafkaOpsService.Has(user, LinkSystemPermissions.CanMigrateKafkaTopics);

    public bool CanView(ClaimsPrincipal user) =>
        _anonymous || KafkaOpsService.Has(user, LinkSystemPermissions.CanViewInfrastructure);

    public async Task<MigrationDryRun> PlanAsync(string topic, int partitions, bool backupSkip, bool backupSkipAcknowledged, IReadOnlyList<string> acknowledgedGroups, CancellationToken cancellationToken, Guid? exceptId = null)
    {
        var facts = await FactsAsync(topic, partitions, backupSkip, backupSkipAcknowledged, acknowledgedGroups, exceptId, cancellationToken);
        return MigrationPreflight.Evaluate(facts);
    }

    public async Task<MigrationRecord> RequestAsync(ClaimsPrincipal user, MigrationRequestBody body, CancellationToken cancellationToken)
    {
        if (!CanMigrate(user))
            throw new KafkaOpsForbiddenException("CanMigrateKafkaTopics is required.");
        if (HasOrphanBlock)
            throw new KafkaOpsRejectedException("An orphan _linkmig topic is present. Resolve it before another Kafka change.");
        var topic = (body.Topic ?? "").Trim();
        var error = MigrationApprovals.ValidateRequest(KafkaOpsService.UserName(user), body.Confirmation, topic, body.Reason);
        if (error is not null)
            throw new KafkaOpsRejectedException(error);
        var dry = await PlanAsync(topic, body.Partitions, body.BackupSkip, body.BackupSkipAcknowledged, body.AcknowledgedGroups, cancellationToken);
        if (!dry.Accepted)
            throw new KafkaOpsRejectedException(dry.Summary);
        var hashError = MigrationApprovals.ValidatePlanHash(body.PlanHash, dry.PlanHash);
        if (hashError is not null)
            throw new KafkaOpsRejectedException(hashError);
        var family = KafkaTopicCatalog.FamilyOf(topic);
        var record = new MigrationRecord
        {
            Id = Guid.NewGuid(),
            Topic = topic,
            OriginalPartitions = body.Partitions > 0 ? await CurrentPartitionsAsync(topic, cancellationToken) : 0,
            TargetPartitions = body.Partitions,
            Step = MigrationStep.Pending,
            PlanHash = dry.PlanHash,
            Requester = KafkaOpsService.UserName(user),
            Reason = body.Reason.Trim(),
            BackupSkipped = body.BackupSkip,
            AcknowledgedGroups = MigrationPreflight.CanonicalGroups(body.AcknowledgedGroups),
            BackupTopic = KafkaTopicCatalog.BackupTopicName(topic, Guid.NewGuid()),
            StopProducers = family.ProducerWorkloads.ToList(),
            StopConsumers = family.ConsumerWorkloads.ToList(),
            SiblingsToGrow = dry.Siblings,
            ManualChecklist = _workloads.ManualChecklist,
            StepStartedUtc = DateTimeOffset.UtcNow
        };
        record.BackupTopic = KafkaTopicCatalog.BackupTopicName(topic, record.Id);
        record.Groups = (await DiscoveredGroupsAsync(topic, cancellationToken)).Select(group => group.GroupId).ToList();
        if (record.OriginalPartitions <= 0)
            record.OriginalPartitions = await CurrentPartitionsAsync(topic, cancellationToken);
        return await WithLease(fence => SaveNewAsync(record, fence, cancellationToken), cancellationToken);
    }

    public async Task<MigrationRecord> ApproveAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        var record = await GetAsync(user, id, mutate: true, cancellationToken);
        var live = await PlanAsync(record.Topic, record.TargetPartitions, record.BackupSkipped, record.BackupSkipped, record.AcknowledgedGroups, cancellationToken);
        var error = MigrationApprovals.ValidateApprove(record, KafkaOpsService.UserName(user), live.PlanHash);
        if (error is not null)
            throw new KafkaOpsRejectedException(error);
        record.Approver = KafkaOpsService.UserName(user);
        record.ApprovedHash = record.PlanHash;
        record.Step = MigrationStep.Approved;
        record.StepSequence++;
        record.Timeline.Add(new MigrationTimelineEntry { Sequence = record.StepSequence, Step = record.Step, Text = "Approved.", At = DateTimeOffset.UtcNow });
        return await WithLease(fence => SaveAsync(record, fence, cancellationToken), cancellationToken);
    }

    public async Task<MigrationRecord> RejectAsync(ClaimsPrincipal user, Guid id, string reason, CancellationToken cancellationToken)
    {
        var record = await GetAsync(user, id, mutate: true, cancellationToken);
        if (record.Step is not (MigrationStep.Pending or MigrationStep.Approved))
            throw new KafkaOpsRejectedException("Only a pending or approved migration can be rejected.");
        if (string.Equals(KafkaOpsService.UserName(user), record.Requester, StringComparison.OrdinalIgnoreCase))
            throw new KafkaOpsRejectedException("A different person must reject the migration.");
        record.Step = MigrationStep.Rejected;
        record.Failure = string.IsNullOrWhiteSpace(reason) ? "Rejected." : reason.Trim();
        record.StepSequence++;
        return await WithLease(fence => SaveAsync(record, fence, cancellationToken), cancellationToken);
    }

    public Task<MigrationRecord> ExecuteAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken) =>
        CommandAsync(user, id, MigrationCommand.Tick, null, cancellationToken, execute: true);

    public Task<MigrationRecord> CommandAsync(ClaimsPrincipal user, Guid id, MigrationCommand command, string? typedName, CancellationToken cancellationToken) =>
        CommandAsync(user, id, command, typedName, cancellationToken, execute: false);

    public async Task<MigrationRecord> ManualStepAsync(ClaimsPrincipal user, Guid id, string workload, CancellationToken cancellationToken)
    {
        var record = await GetAsync(user, id, mutate: true, cancellationToken);
        if (!record.ManualChecklist)
            throw new KafkaOpsRejectedException("Manual checklist mode is not enabled.");
        if (string.IsNullOrWhiteSpace(workload) || !record.StopProducers.Concat(record.StopConsumers).Contains(workload, StringComparer.Ordinal))
            throw new KafkaOpsRejectedException("That workload is not in the stop set.");
        if (!record.ManualConfirmed.Contains(workload, StringComparer.Ordinal))
            record.ManualConfirmed.Add(workload);
        record.StepSequence++;
        record.Timeline.Add(new MigrationTimelineEntry { Sequence = record.StepSequence, Step = record.Step, Text = "Operator confirmed " + workload + ".", At = DateTimeOffset.UtcNow });
        return await WithLease(fence => SaveAsync(record, fence, cancellationToken), cancellationToken);
    }

    public async Task<MigrationRecord> GetAsync(ClaimsPrincipal user, Guid id, bool mutate, CancellationToken cancellationToken)
    {
        if (mutate)
        {
            if (!CanMigrate(user))
                throw new KafkaOpsForbiddenException("CanMigrateKafkaTopics is required.");
        }
        else if (!CanView(user) && !CanMigrate(user))
        {
            throw new KafkaOpsForbiddenException("CanViewInfrastructure is required.");
        }

        var record = await _store.GetAsync(id, cancellationToken);
        if (record is null)
            throw new KafkaOpsNotFoundException("That migration was not found.");
        return record;
    }

    public async Task<string> RunbookAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        var record = await GetAsync(user, id, mutate: false, cancellationToken);
        var lines = new List<string>
        {
            "Topic " + record.Topic + " from " + record.OriginalPartitions + " to " + record.ActiveTarget + " partitions.",
            "Step " + record.Step + ".",
            "The recreated topic starts empty. Consumer groups are committed at offset 0. Nothing is reprocessed.",
            BackupLine(record)
        };
        if (record.Failure.Length > 0)
            lines.Add(record.Failure);
        if (record.Step == MigrationStep.NeedsAttention)
        {
            lines.Add("Choose forward to continue at the target count, or original to recreate the empty topic at the old count. Both require the typed topic name. Nothing is deleted unless that action says so.");
            lines.Add("A foreign topic that already has records is not deleted here. Remove it with kafka-topics.sh --delete --topic " + record.Topic + ", then choose forward.");
            lines.Add("An empty foreign topic is deleted by a second person who types the topic name. That delete does not finish the migration.");
        }
        if (record.Step == MigrationStep.H1)
            lines.Add(record.BackupSkipped
                ? "The executor types the topic name to delete it and continue. Abort rolls back. No temp topic was created."
                : "The executor types the topic name to delete it and continue. Abort rolls back and keeps the backup.");
        foreach (var entry in record.Timeline.TakeLast(12))
            lines.Add(entry.At.ToString("u") + " " + entry.Step + " " + entry.Text);
        return string.Join("\n", lines);
    }

    private static string BackupLine(MigrationRecord record)
    {
        if (record.BackupSkipped)
            return "The backup was skipped. No temp topic was created.";
        if (record.BackupTopic.Length == 0)
            return "Backup (none).";
        if (record.BackupCleanupRequired)
            return "Backup " + record.BackupTopic + " is kept and flagged for cleanup.";
        return "Backup " + record.BackupTopic + " is the verified backup and is kept until a person deletes it or the retention expires.";
    }

    public async Task DeleteBackupAsync(ClaimsPrincipal user, string name, string typedName, CancellationToken cancellationToken)
    {
        if (!CanMigrate(user))
            throw new KafkaOpsForbiddenException("CanMigrateKafkaTopics is required.");
        if (!string.Equals(typedName, name, StringComparison.Ordinal))
            throw new KafkaOpsRejectedException("Type the backup topic name to delete it.");
        if (!name.StartsWith("_linkmig-", StringComparison.Ordinal) || string.Equals(name, KafkaTopicCatalog.JournalTopicName, StringComparison.Ordinal))
            throw new KafkaOpsRejectedException("Only a migration backup topic can be deleted here.");
        var records = await _store.ListAsync(cancellationToken);
        if (records.Any(record => record.BackupTopic == name && record.Step is not (MigrationStep.Done or MigrationStep.RolledBack or MigrationStep.Rejected)))
            throw new KafkaOpsRejectedException("The backup belongs to an open migration.");
        await _admin.DeleteTopicAsync(name, cancellationToken);
        foreach (var record in records.Where(record => record.BackupTopic == name))
        {
            record.BackupCleanupRequired = false;
            record.Evidence = "The backup was deleted by " + KafkaOpsService.UserName(user) + ".";
            await WithLease(fence => SaveAsync(record, fence, cancellationToken), cancellationToken);
        }
    }

    public Task<IReadOnlyCollection<string>> HoldsAsync(CancellationToken cancellationToken) =>
        _holds.HeldTopicsAsync(cancellationToken);

    public async Task<TopicDetailModel> DetailAsync(string topic, CancellationToken cancellationToken)
    {
        var family = KafkaTopicCatalog.FamilyOf(topic);
        var entry = KafkaTopicCatalog.Find(topic);
        var model = new TopicDetailModel
        {
            Topic = topic,
            KeyClass = entry?.KeyClass.ToString() ?? "",
            KeyShape = entry?.KeyShape ?? "",
            Slice1Eligible = family.Slice1Eligible,
            Eligibility = family.Slice1Eligible ? "Eligible for an increase migration." : family.IneligibleReason,
            Producers = family.Producers.Select(site => site.Workload + " " + site.Path).ToList(),
            Consumers = family.Consumers.Select(site => site.Workload + " " + site.Path).ToList(),
            StopSet = family.StopSet.ToList(),
            Held = _holds.IsHeld(topic),
            OpenMigration = _holds.MigrationFor(topic),
            TopicsFilePartitions = 3
        };
        try
        {
            var facts = await _admin.DescribeAsync(topic, cancellationToken);
            if (facts is null)
            {
                model.Error = "Unknown topic or partition";
                return model;
            }

            model.TopicId = facts.TopicId;
            model.Partitions = facts.Partitions;
            model.ReplicationFactor = facts.ReplicationFactor;
            model.FullIsr = facts.FullIsr;
            model.LeadersSkewed = facts.LeadersSkewed;
            model.PartitionsDetail = facts.PartitionRows;
            model.PartitionDrift = facts.Partitions > 0 && facts.Partitions != 3;
            model.EstimatedRecords = facts.HighWatermarks.Zip(facts.LogStarts, (high, low) => Math.Max(0, high - low)).Sum();
            foreach (var group in entry?.Groups ?? [])
            {
                var described = await _admin.DescribeGroupAsync(group, topic, facts.Partitions, cancellationToken);
                if (described is not null)
                    model.Groups.Add(described);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            model.Error = "Kafka could not be read. " + ex.Message;
        }

        return model;
    }

    public async Task<TopicConfigDiffModel> ConfigsAsync(string topic, string? diff, CancellationToken cancellationToken)
    {
        var model = new TopicConfigDiffModel { Topic = topic, Diff = diff ?? "" };
        model.Configs = (await _admin.DescribeConfigsAsync(topic, cancellationToken)).ToList();
        if (string.Equals(diff, "sibling", StringComparison.OrdinalIgnoreCase))
        {
            var sibling = KafkaTopicCatalog.ErrorName(topic);
            var other = await _admin.DescribeConfigsAsync(sibling, cancellationToken);
            model.Changes = Diff(model.Configs, other, sibling);
        }
        else if (string.Equals(diff, "frozen", StringComparison.OrdinalIgnoreCase))
        {
            var open = (await _store.ListAsync(cancellationToken)).FirstOrDefault(record => record.Topic == topic && record.FrozenConfigs.Count > 0);
            if (open is null)
                model.Changes.Add("No frozen migration snapshot is stored for this topic.");
            else
                model.Changes = Diff(model.Configs, open.FrozenConfigs.Select(pair => new TopicConfigRow { Name = pair.Key, Value = pair.Value }), "frozen");
        }
        else if (string.Equals(diff, "broker", StringComparison.OrdinalIgnoreCase))
        {
            var broker = await _admin.DescribeBrokerConfigAsync(0, cancellationToken);
            foreach (var row in model.Configs)
            {
                if (broker.TryGetValue(row.Name, out var value) && !string.Equals(value, row.Value, StringComparison.Ordinal))
                    model.Changes.Add(row.Name + " topic=" + row.Value + " broker=" + value);
            }
        }
        else if (string.Equals(diff, "topics", StringComparison.OrdinalIgnoreCase))
        {
            model.Changes.Add("topics.txt declares partition count 3 and does not declare topic configs.");
        }

        return model;
    }

    public async Task TickOpenAsync(long fence, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _reconciled) == 0)
            await ReconcileAsync(cancellationToken);
        foreach (var record in await _store.ListAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (record.Step is MigrationStep.Done or MigrationStep.RolledBack or MigrationStep.Rejected)
            {
                await ExpireBackupAsync(record, fence, cancellationToken);
                continue;
            }

            // Planned, pending, and approved wait for a person. NeedsAttention waits for a recover command.
            // The hold point is ticked so its timeout can fire. The tick does not write a timeline note.
            if (record.Step is MigrationStep.Planned or MigrationStep.Pending or MigrationStep.Approved or MigrationStep.NeedsAttention)
                continue;
            try
            {
                await DriveAsync(record, MigrationCommand.Tick, record.Executor, null, fence, cancellationToken, 12);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Migration {MigrationId} tick failed at {Step}", record.Id, record.Step);
            }
        }
    }

    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        try
        {
            var journal = await _admin.ReadJournalAsync(cancellationToken);
            var stored = await _store.ListAsync(cancellationToken);
            var byId = stored.ToDictionary(record => record.Id);
            foreach (var entry in journal)
            {
                if (!byId.TryGetValue(entry.Id, out var current) || entry.StepSequence > current.StepSequence)
                    byId[entry.Id] = entry;
            }

            foreach (var record in byId.Values)
                await _store.SaveAsync(record, Math.Max(record.Fence, 1), cancellationToken);
            await ArmHoldsAsync(byId.Values, cancellationToken);
            var names = await _admin.ListLinkMigTopicsAsync(cancellationToken);
            var known = new HashSet<string>(byId.Values.Select(record => record.BackupTopic).Append(KafkaTopicCatalog.JournalTopicName), StringComparer.Ordinal);
            HasOrphanBlock = names.Any(name => !known.Contains(name));
            Interlocked.Exchange(ref _reconciled, 1);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Migration reconcile did not finish. New migrations stay closed until the cluster can be read.");
        }
    }

    private async Task<MigrationRecord> CommandAsync(ClaimsPrincipal user, Guid id, MigrationCommand command, string? typedName, CancellationToken cancellationToken, bool execute)
    {
        var record = await GetAsync(user, id, mutate: true, cancellationToken);
        if (execute)
        {
            var error = MigrationApprovals.ValidateExecute(record, KafkaOpsService.UserName(user));
            if (error is not null)
                throw new KafkaOpsRejectedException(error);
            record.Executor = KafkaOpsService.UserName(user);
            record.Step = MigrationStep.A1;
            record.StepStartedUtc = DateTimeOffset.UtcNow;
            command = MigrationCommand.Tick;
        }

        var actor = KafkaOpsService.UserName(user);
        var requested = command;
        var steps = requested == MigrationCommand.Go && !execute ? 1 : 40;
        var result = await WithLease(fence => DriveAsync(record, command, actor, typedName, fence, cancellationToken, steps), cancellationToken);
        if (requested == MigrationCommand.Go && result.Step is not (MigrationStep.Done or MigrationStep.RolledBack or MigrationStep.Rejected or MigrationStep.NeedsAttention or MigrationStep.H1))
            Continue(result.Id);
        return result;
    }

    private void Continue(Guid id)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var record = await _store.GetAsync(id, CancellationToken.None);
                if (record is null)
                    return;
                for (var n = 0; n < 80 && StepRuns(record.Step); n++)
                {
                    var before = record.StepSequence;
                    record = await WithLease(fence => DriveAsync(record, MigrationCommand.Tick, record.Executor, null, fence, CancellationToken.None, 1), CancellationToken.None);
                    if (!StepRuns(record.Step))
                        return;
                    if (record.StepSequence == before)
                        await Task.Delay(500);
                }
            }
            catch (LeaseHeldException)
            {
                _logger.LogDebug("Migration {MigrationId} continuation waited for the executor lease.", id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Migration {MigrationId} continuation stopped.", id);
            }
        });
    }

    private static bool StepRuns(MigrationStep step) =>
        step is not (MigrationStep.Done or MigrationStep.RolledBack or MigrationStep.Rejected or MigrationStep.NeedsAttention or MigrationStep.H1
            or MigrationStep.Planned or MigrationStep.Pending or MigrationStep.Approved);

    private static bool StepOpen(MigrationStep step, bool admitRecover)
    {
        if (step is MigrationStep.Done or MigrationStep.RolledBack or MigrationStep.Rejected)
            return false;
        if (step == MigrationStep.NeedsAttention)
            return admitRecover;
        return true;
    }

    private async Task<MigrationRecord> DriveAsync(MigrationRecord record, MigrationCommand command, string? actor, string? typed, long fence, CancellationToken cancellationToken, int maxSteps)
    {
        var admitRecover = command is MigrationCommand.RecoverForward or MigrationCommand.RecoverOriginal or MigrationCommand.DeleteForeign;
        for (var i = 0; i < maxSteps && StepOpen(record.Step, admitRecover); i++)
        {
            admitRecover = false;
            var seen = await ObserveAsync(record, cancellationToken);
            var tick = MigrationMachine.Describe(record, seen, command, actor, typed, DateTimeOffset.UtcNow, Limits());
            command = MigrationCommand.Tick;
            typed = null;
            if (tick.Error is not null)
                throw new KafkaOpsRejectedException(tick.Error);
            if (tick.Effects.Count > 0)
            {
                var intent = tick.Persist ?? record;
                await SaveAsync(intent, fence, cancellationToken);
                await ApplyAsync(intent, tick.Effects, cancellationToken);
                var after = await ObserveAsync(intent, cancellationToken);
                var resume = MigrationMachine.Describe(intent, after, MigrationCommand.Tick, record.Executor, null, DateTimeOffset.UtcNow, Limits());
                record = resume.Completed ?? resume.Persist ?? intent;
                await SaveAsync(record, fence, cancellationToken);
                if (resume.Error is not null)
                    throw new KafkaOpsRejectedException(resume.Error);
                if (resume.Wait || record.Step is MigrationStep.NeedsAttention)
                    break;
                continue;
            }

            if (tick.Completed is not null)
            {
                record = tick.Completed;
                await SaveAsync(record, fence, cancellationToken);
                continue;
            }

            if (tick.Persist is not null)
            {
                record = tick.Persist;
                await SaveAsync(record, fence, cancellationToken);
                if (tick.Wait)
                    break;
                continue;
            }

            break;
        }

        return record;
    }

    private async Task ApplyAsync(MigrationRecord record, IReadOnlyList<MigrationEffect> effects, CancellationToken cancellationToken)
    {
        foreach (var effect in effects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (effect)
            {
                case MigrationEffect.CreateTopic create:
                    await _admin.CreateTopicAsync(create.Name, create.Partitions, create.ReplicationFactor, create.Configs, validateOnly: false, cancellationToken);
                    break;
                case MigrationEffect.DeleteTopic delete:
                    await _admin.DeleteTopicAsync(delete.Name, cancellationToken);
                    break;
                case MigrationEffect.GrowPartitions grow:
                    await _admin.GrowPartitionsAsync(grow.Name, grow.To, cancellationToken);
                    break;
                case MigrationEffect.ScaleWorkload scale:
                    if (!record.ManualChecklist)
                        await _workloads.ScaleAsync(scale.Workload, scale.Replicas, cancellationToken);
                    break;
                case MigrationEffect.SetHold hold:
                    _holds.Set(record.Topic, record.Id, hold.Active);
                    break;
                case MigrationEffect.CopyLog:
                    var outcome = await _admin.CopyBackupAsync(record, cancellationToken);
                    if (!outcome.Ok)
                        _copyFailures[record.Id] = outcome.Failure;
                    else
                        _copyFailures.Remove(record.Id);
                    break;
                case MigrationEffect.SetRetention retention:
                    await _admin.SetRetentionAsync(retention.Topic, retention.RetentionMs, cancellationToken);
                    break;
                case MigrationEffect.WriteOffsets write:
                    await _admin.WriteOffsetsAsync(write.Group, record.Topic, write.Offsets, cancellationToken);
                    break;
                case MigrationEffect.ElectLeaders elect:
                    await _admin.ElectLeadersAsync(elect.Topic, cancellationToken);
                    break;
            }
        }
    }

    private async Task<MigrationObservation> ObserveAsync(MigrationRecord record, CancellationToken cancellationToken)
    {
        var seen = new MigrationObservation
        {
            LivePlanHash = record.PlanHash,
            PreflightOk = true,
            ClusterHealthy = true,
            HoldsActive = _holds.IsHeld(record.Topic)
        };
        if (record.Step == MigrationStep.A1)
        {
            var dry = await PlanAsync(record.Topic, record.TargetPartitions, record.BackupSkipped, record.BackupSkipped, record.AcknowledgedGroups, cancellationToken, record.Id);
            seen.PreflightOk = dry.Accepted;
            seen.PreflightError = dry.Accepted ? "" : dry.Summary;
            seen.LivePlanHash = dry.PlanHash;
        }

        try
        {
            seen.ClusterHealthy = await _admin.ClusterHealthyAsync(cancellationToken);
            var topic = await _admin.DescribeAsync(record.Topic, cancellationToken);
            seen.TopicPresent = topic is not null;
            if (topic is not null)
            {
                seen.TopicId = topic.TopicId;
                seen.Partitions = topic.Partitions;
                seen.ReplicationFactor = topic.ReplicationFactor;
                seen.FullIsr = topic.FullIsr;
                seen.LeadersSkewed = topic.LeadersSkewed;
                seen.HighWatermarks = topic.HighWatermarks;
                seen.Configs = topic.Configs;
                seen.OldestCreateTimeMs = topic.OldestCreateTimeMs;
            }

            if (record.BackupTopic.Length > 0)
            {
                var backup = await _admin.DescribeAsync(record.BackupTopic, cancellationToken);
                seen.BackupPresent = backup is not null;
                if (backup is not null)
                {
                    seen.BackupId = backup.TopicId;
                    seen.BackupPartitions = backup.Partitions;
                    seen.BackupReplicationFactor = backup.ReplicationFactor;
                    seen.BackupEmpty = backup.HighWatermarks.All(mark => mark == 0);
                    var expectedRf = Math.Max(record.ReplicationFactor, Math.Min(3, topic?.ReplicationFactor ?? record.ReplicationFactor));
                    seen.BackupSpecMatches = backup.Partitions == record.TargetPartitions && backup.ReplicationFactor == expectedRf;
                }
            }

            foreach (var sibling in record.SiblingsToGrow)
            {
                var described = await _admin.DescribeAsync(sibling, cancellationToken);
                seen.SiblingPartitions[sibling] = described?.Partitions ?? 0;
            }

            foreach (var name in record.StopProducers.Concat(record.StopConsumers).Distinct(StringComparer.Ordinal))
            {
                var replicas = await _workloads.ReadReplicasAsync(name, cancellationToken);
                if (replicas is { } count)
                    seen.WorkloadReplicas[name] = count;
            }

            foreach (var group in record.Groups)
            {
                var described = await _admin.DescribeGroupAsync(group, record.Topic, Math.Max(seen.Partitions, record.ActiveTarget), cancellationToken);
                if (described is null)
                    continue;
                seen.Groups[group] = new GroupObservation
                {
                    State = described.State,
                    Members = described.Members,
                    Lag = MigrationGroups.Lag(seen.HighWatermarks, described.Committed),
                    Committed = described.Committed
                };
            }

            seen.ConsumersStable = record.StopConsumers.All(name => seen.WorkloadReplicas.GetValueOrDefault(name) > 0)
                && seen.Groups.Values.All(group => MigrationGroups.Ready(group.State, group.Members, group.Committed));
            if (record.Step is MigrationStep.B6 or MigrationStep.B7 && !record.BackupSkipped)
            {
                var inspection = await _admin.InspectBackupAsync(record, cancellationToken);
                seen.BackupVerified = inspection.Ok && inspection.Written.Count == 0 && inspection.ReachedEnd && inspection.CountsMatch && inspection.DigestsMatch;
                seen.CountsMatch = inspection.CountsMatch;
                seen.DigestMatch = inspection.DigestsMatch;
                seen.HeaderConsistent = inspection.Ok;
                if (!inspection.Ok)
                {
                    seen.CopyFailed = true;
                    seen.CopyFailure = inspection.Failure;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            seen.ClusterHealthy = false;
            if (record.Step == MigrationStep.A1)
            {
                seen.PreflightOk = false;
                seen.PreflightError = ex.Message;
            }
        }

        if (_copyFailures.TryGetValue(record.Id, out var failure))
        {
            seen.CopyFailed = true;
            seen.CopyFailure = failure;
        }

        return seen;
    }

    private async Task<MigrationFacts> FactsAsync(string topic, int partitions, bool backupSkip, bool backupSkipAcknowledged, IReadOnlyList<string> acknowledgedGroups, Guid? exceptId, CancellationToken cancellationToken)
    {
        var described = await _admin.DescribeAsync(topic, cancellationToken);
        var family = KafkaTopicCatalog.FamilyOf(topic);
        var acked = new HashSet<string>(acknowledgedGroups, StringComparer.Ordinal);
        var groups = new List<GroupFact>();
        var rightsOk = OperationsAllow(described?.AuthorizedOperations, "Read", "Write", "Create", "Delete", "Alter", "Describe", "DescribeConfigs");
        try
        {
            foreach (var discovered in await DiscoveredGroupsAsync(topic, cancellationToken))
            {
                if (!OperationsAllow(discovered.AuthorizedOperations, "Read"))
                    rightsOk = false;
                groups.Add(new GroupFact
                {
                    GroupId = discovered.GroupId,
                    Active = discovered.Members > 0 || string.Equals(discovered.State, "Stable", StringComparison.OrdinalIgnoreCase),
                    Mapped = (KafkaTopicCatalog.Find(topic)?.Groups ?? []).Contains(discovered.GroupId, StringComparer.Ordinal),
                    Lag = MigrationGroups.Lag(described?.HighWatermarks ?? [], discovered.Committed),
                    Acknowledged = acked.Contains(discovered.GroupId)
                });
            }
        }
        catch (KafkaOpsRejectedException ex)
        {
            groups.Add(new GroupFact { GroupId = "discovery", Active = true, Mapped = false, Lag = 0 });
            _logger.LogWarning("Group discovery skipped: {Reason}", ex.Message);
        }

        var bytes = described is null ? 0 : described.HighWatermarks.Zip(described.LogStarts, (high, low) => Math.Max(0, high - low)).Sum() * 256L;
        var stop = family.StopSet;
        var mapped = _workloads.ManualChecklist || stop.All(name => _workloads.TryMap(name, out _));
        var owns = false;
        if (string.Equals(_options.InfraProvider, "Strimzi", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                owns = await _workloads.TopicOperatorOwnsAsync(topic, cancellationToken);
            }
            catch (KafkaOpsRejectedException)
            {
                owns = true;
            }
        }

        var openMigration = (await _store.ListAsync(cancellationToken)).Any(record => record.Id != exceptId && record.Step is not (MigrationStep.Done or MigrationStep.RolledBack or MigrationStep.Rejected));
        var reassignment = await _infra.ListInFlightReassignmentsAsync(cancellationToken);
        return new MigrationFacts
        {
            Topic = topic,
            CurrentPartitions = described?.Partitions ?? 0,
            RequestedPartitions = partitions,
            Cap = _options.MaxPartitionsPerTopic,
            ReadOnly = _readOnly,
            AllowTopicMigration = _options.AllowTopicMigration,
            Provider = _options.InfraProvider,
            StoreDurable = _store.Durable,
            OtherOpen = openMigration || await OtherChangeOpenAsync(cancellationToken),
            ReassignmentKnown = reassignment.Known,
            ReassignmentEmpty = reassignment.Topics.Count == 0 && reassignment.Partitions.Count == 0,
            TopicOperatorManages = _options.TopicOperatorManagesLinkTopics,
            KafkaTopicResourceNamesTopic = owns,
            DedicatedConnection = _admin.DedicatedConnection,
            RightsOk = rightsOk,
            ClusterHealthy = described is not null && await _admin.ClusterHealthyAsync(cancellationToken),
            FullIsr = described?.FullIsr ?? false,
            ReplicationFactor = described?.ReplicationFactor ?? 0,
            MinInSyncReplicas = described?.EffectiveMinInSyncReplicas ?? 0,
            CleanupPolicy = string.IsNullOrWhiteSpace(described?.EffectiveCleanupPolicy) ? "delete" : described!.EffectiveCleanupPolicy,
            EstimatedBytes = bytes,
            EstimatedBackupMinutes = (int)Math.Max(1, bytes / (2 * 1024 * 1024)),
            MaxBackupMinutes = _options.MigrationMaxBackupMinutes,
            BackupSkip = backupSkip,
            BackupSkipAcknowledged = backupSkipAcknowledged,
            AcknowledgedGroupIds = MigrationPreflight.CanonicalGroups(acknowledgedGroups),
            DiskKnown = false,
            UnknownDiskMaxBytes = _options.MigrationUnknownDiskMaxBytes,
            WorkloadsMapped = mapped,
            DrainMinutes = 1,
            MetadataRefreshIntervalMs = _options.MetadataRefreshIntervalMs,
            Groups = groups,
            StopSet = stop.ToList(),
            Siblings = MigrationPreflight.SiblingsFor(family, growDotNetError: true),
            SizeClass = bytes < 10_000_000 ? "small" : bytes < 100_000_000 ? "medium" : "large"
        };
    }

    private static bool OperationsAllow(IReadOnlyList<string>? granted, params string[] required)
    {
        // A broker with no authorizer returns an empty list. That is not a missing grant.
        // A non-empty list that omits a required operation fails closed.
        if (granted is null || granted.Count == 0)
            return true;
        if (granted.Any(operation => operation.Equals("All", StringComparison.OrdinalIgnoreCase)))
            return true;
        return required.All(need => granted.Any(operation => operation.Equals(need, StringComparison.OrdinalIgnoreCase)));
    }

    private async Task<IReadOnlyList<MigrationGroupFacts>> DiscoveredGroupsAsync(string topic, CancellationToken cancellationToken)
    {
        var names = await _admin.ListGroupsWhenHealthyAsync(cancellationToken);
        var described = await _admin.DescribeAsync(topic, cancellationToken);
        var partitions = described?.Partitions ?? 0;
        var found = new List<MigrationGroupFacts>();
        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var group = await _admin.DescribeGroupAsync(name, topic, partitions, cancellationToken);
            if (group is not null && group.Committed.Any(offset => offset >= 0))
                found.Add(group);
        }

        foreach (var catalogGroup in KafkaTopicCatalog.Find(topic)?.Groups ?? [])
        {
            if (found.All(group => !string.Equals(group.GroupId, catalogGroup, StringComparison.Ordinal)))
                found.Add(new MigrationGroupFacts { GroupId = catalogGroup, State = "Empty" });
        }

        return found;
    }

    private async Task<int> CurrentPartitionsAsync(string topic, CancellationToken cancellationToken)
    {
        var described = await _admin.DescribeAsync(topic, cancellationToken);
        return described?.Partitions ?? 0;
    }

    private async Task<bool> OtherChangeOpenAsync(CancellationToken cancellationToken)
    {
        var ids = await _cache.GetAsync<List<Guid>>(KafkaOpsService.IndexKey(_environment.EnvironmentName), cancellationToken) ?? [];
        foreach (var id in ids)
        {
            var record = await _cache.GetAsync<ChangeRequestRecord>(KafkaOpsService.RequestStorageKey(_environment.EnvironmentName, id), cancellationToken);
            if (record is not null && record.Status is KafkaChangeStatus.Pending or KafkaChangeStatus.Approved or KafkaChangeStatus.Executing or KafkaChangeStatus.Converging or KafkaChangeStatus.Verifying)
                return true;
        }

        return false;
    }

    private async Task ArmHoldsAsync(IEnumerable<MigrationRecord> records, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var record in records)
        {
            _holds.Set(record.Topic, record.Id, MigrationHoldRegistry.Armed(record.Step));
        }
    }

    private async Task ExpireBackupAsync(MigrationRecord record, long fence, CancellationToken cancellationToken)
    {
        if (record.BackupTopic.Length == 0 || record.BackupSkipped)
            return;
        var created = record.Timeline.FirstOrDefault()?.At ?? record.StepStartedUtc ?? DateTimeOffset.UtcNow;
        if (DateTimeOffset.UtcNow - created < TimeSpan.FromHours(Math.Max(1, _options.MigrationBackupRetentionHours)))
            return;
        try
        {
            await _admin.DeleteTopicAsync(record.BackupTopic, cancellationToken);
            record.BackupCleanupRequired = false;
            record.Evidence = "Backup retention expired and the temp topic was deleted.";
            await SaveAsync(record, fence, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Backup retention delete failed for {Topic}", record.BackupTopic);
        }
    }

    private MigrationLimits Limits() => new()
    {
        HoldMinutes = _options.MigrationHoldMinutes,
        MaxBackupMinutes = _options.MigrationMaxBackupMinutes,
        BackupRetentionHours = _options.MigrationBackupRetentionHours,
        MetadataRefreshIntervalMs = _options.MetadataRefreshIntervalMs
    };

    private async Task<MigrationRecord> SaveNewAsync(MigrationRecord record, long fence, CancellationToken cancellationToken) =>
        await SaveAsync(record, fence, cancellationToken);

    private async Task<MigrationRecord> SaveAsync(MigrationRecord record, long fence, CancellationToken cancellationToken)
    {
        await _store.SaveAsync(record, fence, cancellationToken);
        try
        {
            await _admin.AppendJournalAsync(record, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The migration journal did not accept {MigrationId}", record.Id);
        }

        return record;
    }

    private async Task<MigrationRecord> WithLease(Func<long, Task<MigrationRecord>> action, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (true)
        {
            try
            {
                await using var hold = await _lease.AcquireAsync(cancellationToken);
                return await action(hold.Fence);
            }
            catch (LeaseHeldException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            }
        }
    }

    private static List<string> Diff(IEnumerable<TopicConfigRow> left, IEnumerable<TopicConfigRow> right, string label)
    {
        var other = right.ToDictionary(row => row.Name, row => row.Value, StringComparer.OrdinalIgnoreCase);
        var changes = new List<string>();
        foreach (var row in left)
        {
            if (!other.TryGetValue(row.Name, out var value))
                continue;
            if (!string.Equals(row.Value, value, StringComparison.Ordinal))
                changes.Add(row.Name + " topic=" + row.Value + " " + label + "=" + value);
        }

        return changes;
    }
}
