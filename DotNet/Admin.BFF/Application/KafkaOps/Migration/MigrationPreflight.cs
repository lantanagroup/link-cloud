using System.Security.Cryptography;
using System.Text;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public sealed class GroupFact
{
    public string GroupId { get; set; } = "";
    public bool Active { get; set; }
    public bool Mapped { get; set; } = true;
    public long Lag { get; set; }
    public bool Acknowledged { get; set; }
}

public sealed class MigrationFacts
{
    public string Topic { get; set; } = "";
    public int CurrentPartitions { get; set; }
    public int RequestedPartitions { get; set; }
    public int Cap { get; set; } = 24;
    public bool ReadOnly { get; set; }
    public bool AllowTopicMigration { get; set; }
    public string Provider { get; set; } = "Disabled";
    public bool StoreDurable { get; set; }
    public bool OtherOpen { get; set; }
    public bool ReassignmentKnown { get; set; }
    public bool ReassignmentEmpty { get; set; } = true;
    public bool TopicOperatorManages { get; set; }
    public bool KafkaTopicResourceNamesTopic { get; set; }
    public bool DedicatedConnection { get; set; }
    public bool RightsOk { get; set; } = true;
    public bool ClusterHealthy { get; set; } = true;
    public bool FullIsr { get; set; } = true;
    public int BrokerCount { get; set; } = 3;
    public int ReplicationFactor { get; set; } = 3;
    public int MinInSyncReplicas { get; set; } = 2;
    public bool AutoCreate { get; set; }
    public string CleanupPolicy { get; set; } = "delete";
    public long EstimatedBytes { get; set; }
    public int EstimatedBackupMinutes { get; set; }
    public int MaxBackupMinutes { get; set; } = 15;
    public bool BackupSkip { get; set; }
    public bool BackupSkipAcknowledged { get; set; }
    public long? FreeDiskBytes { get; set; }
    public long UnknownDiskMaxBytes { get; set; } = 1_073_741_824;
    public bool DiskKnown { get; set; }
    public bool WorkloadsMapped { get; set; } = true;
    public int DrainMinutes { get; set; }
    public int MetadataRefreshIntervalMs { get; set; } = 60_000;
    public List<GroupFact> Groups { get; set; } = [];
    public List<string> AcknowledgedGroupIds { get; set; } = [];
    public List<string> StopSet { get; set; } = [];
    public List<string> Siblings { get; set; } = [];
    public string SizeClass { get; set; } = "small";
}

public sealed class MigrationDryRun
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

public static class MigrationPreflight
{
    public static MigrationDryRun Evaluate(MigrationFacts facts)
    {
        var result = new MigrationDryRun();
        var family = KafkaTopicCatalog.FamilyOf(facts.Topic);
        var entry = KafkaTopicCatalog.Find(facts.Topic);
        result.StopSet = facts.StopSet.Count > 0 ? [.. facts.StopSet] : [.. family.StopSet];
        result.Siblings = facts.Siblings.Count > 0 ? [.. facts.Siblings] : SiblingsFor(family, growDotNetError: true);

        if (facts.ReadOnly)
            result.Errors.Add("Kafka changes are read-only in this environment.");
        if (!facts.AllowTopicMigration)
            result.Errors.Add("Topic migration is disabled. Set KafkaOps:AllowTopicMigration to true to allow it.");
        if (!string.Equals(facts.Provider, "LocalCompose", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(facts.Provider, "Strimzi", StringComparison.OrdinalIgnoreCase))
            result.Errors.Add("The infrastructure provider cannot migrate topics. Disabled refuses because reassignment state is unknown.");
        if (!facts.StoreDurable && !string.Equals(facts.Provider, "LocalCompose", StringComparison.OrdinalIgnoreCase))
            result.Errors.Add("An in-memory operations store cannot hold a migration. LocalCompose is the only exception.");
        if (!facts.DedicatedConnection && !string.Equals(facts.Provider, "LocalCompose", StringComparison.OrdinalIgnoreCase))
            result.Errors.Add("A dedicated KafkaOps connection is required outside LocalCompose.");
        if (facts.OtherOpen)
            result.Errors.Add("Another change or migration is already open in this environment.");
        if (!facts.ReassignmentKnown || !facts.ReassignmentEmpty)
            result.Errors.Add("In-flight reassignments must be known and empty.");
        if (facts.TopicOperatorManages || facts.KafkaTopicResourceNamesTopic)
            result.Errors.Add("A KafkaTopic resource manages this topic. The topic operator would recreate it.");
        if (!facts.RightsOk)
            result.Errors.Add("The KafkaOps principal is missing a required topic or group operation.");
        if (!facts.ClusterHealthy)
            result.Errors.Add("The cluster is not healthy.");
        if (!facts.FullIsr)
            result.Errors.Add("The topic or a sibling is under-replicated.");
        if (facts.BrokerCount < facts.ReplicationFactor)
            result.Errors.Add("The broker count is below the topic replication factor.");
        if (facts.MinInSyncReplicas < 1)
            result.Errors.Add("min.insync.replicas could not be read.");
        else if (facts.MinInSyncReplicas > facts.ReplicationFactor)
            result.Errors.Add("min.insync.replicas cannot be met.");
        if (facts.AutoCreate)
            result.Warnings.Add("auto.create.topics.enable is true. Recreate checks the topic id before adopting T.");
        if (entry is null)
            result.Errors.Add("The topic is not in the catalog.");
        else if (!family.Slice1Eligible)
            result.Errors.Add(string.IsNullOrWhiteSpace(family.IneligibleReason) ? "The topic is not eligible in slice 1." : family.IneligibleReason);
        if (entry?.HardBlocked == true)
            result.Errors.Add(entry.BlockReason);
        if (facts.RequestedPartitions <= facts.CurrentPartitions)
            result.Errors.Add("The new partition count must be higher than the current count.");
        if (facts.Cap > 0 && facts.RequestedPartitions > facts.Cap)
            result.Errors.Add($"The new partition count must be at most {facts.Cap}.");
        if (!string.IsNullOrWhiteSpace(facts.CleanupPolicy) && !string.Equals(facts.CleanupPolicy, "delete", StringComparison.OrdinalIgnoreCase))
            result.Errors.Add("Compacted topics are refused in slice 1.");
        if (!facts.WorkloadsMapped)
            result.Errors.Add("A stop-set workload has no provider target.");

        foreach (var group in facts.Groups)
        {
            if (group.Active && !group.Mapped)
                result.Errors.Add($"Group {group.GroupId} is active and is not in the stop set.");
            if (!group.Active && group.Lag > 0 && !group.Acknowledged)
                result.Errors.Add($"Group {group.GroupId} is inactive and has lag. Type the group name to drop its offsets on this topic.");
        }

        if (facts.DrainMinutes >= 5)
            result.Errors.Add("The estimated drain is 5 minutes or more.");

        var backupMinutes = facts.BackupSkip ? 0 : facts.EstimatedBackupMinutes;
        if (!facts.BackupSkip && facts.EstimatedBackupMinutes > facts.MaxBackupMinutes)
            result.Errors.Add($"The estimated copy exceeds {facts.MaxBackupMinutes} minutes. Backup skip requires a typed acknowledgment.");
        if (facts.BackupSkip && !facts.BackupSkipAcknowledged)
            result.Errors.Add("Skipping the backup requires a typed acknowledgment from the requester and the approver.");

        var bytes = facts.BackupSkip ? 0 : facts.EstimatedBytes;
        var needed = bytes * Math.Max(1, facts.ReplicationFactor) * 3 / 2;
        if (facts.DiskKnown && facts.FreeDiskBytes is { } free && free < needed)
            result.Errors.Add("Free disk is below the backup size times the replication factor times 1.5.");
        if (!facts.DiskKnown && !string.Equals(facts.Provider, "LocalCompose", StringComparison.OrdinalIgnoreCase) && bytes > facts.UnknownDiskMaxBytes)
            result.Errors.Add("Without disk metrics the backup must stay under the unknown-disk cap.");

        var quietMinutes = Math.Max(1, facts.MetadataRefreshIntervalMs * 2 / 60_000);
        result.WindowMinutes = 1 + quietMinutes + Math.Max(0, facts.DrainMinutes) + 1 + backupMinutes + 2 + 2;
        result.AlertMinutes = (int)Math.Ceiling(result.WindowMinutes * 1.5);
        result.PlanHash = Hash(facts, result.StopSet, result.Siblings);
        result.Accepted = result.Errors.Count == 0;
        result.Summary = result.Accepted
            ? $"Migrate {facts.Topic} from {facts.CurrentPartitions} to {facts.RequestedPartitions}. Window about {result.WindowMinutes} minutes, alert at {result.AlertMinutes}."
            : string.Join(" ", result.Errors);
        return result;
    }

    public static List<string> CanonicalGroups(IEnumerable<string>? names) =>
        (names ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    public static string Hash(MigrationFacts facts, IReadOnlyList<string> stopSet, IReadOnlyList<string> siblings)
    {
        var groups = facts.Groups
            .OrderBy(group => group.GroupId, StringComparer.Ordinal)
            .Select(group => group.GroupId + ":" + (group.Lag > 0 ? "lag" : "zero"));
        var acknowledged = CanonicalGroups(
            facts.AcknowledgedGroupIds.Concat(
                facts.Groups.Where(group => group.Acknowledged).Select(group => group.GroupId)));
        var canonical = string.Join("|",
            facts.Topic,
            facts.CurrentPartitions.ToString(),
            facts.RequestedPartitions.ToString(),
            string.Join(",", stopSet.OrderBy(name => name, StringComparer.Ordinal)),
            string.Join(",", groups),
            facts.SizeClass,
            string.Join(",", siblings.OrderBy(name => name, StringComparer.Ordinal)),
            facts.BackupSkip ? "skip" : "backup",
            facts.ReplicationFactor.ToString(),
            facts.CleanupPolicy);
        if (acknowledged.Count > 0)
            canonical += "|ack=" + string.Join(",", acknowledged);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static List<string> SiblingsFor(KafkaTopicFamily family, bool growDotNetError)
    {
        var names = new List<string>();
        if (family.JavaPinnedSiblings)
        {
            names.Add(KafkaTopicCatalog.RetryName(family.Topic));
            names.Add(KafkaTopicCatalog.ErrorName(family.Topic));
        }
        else if (growDotNetError && family.Topic.Length > 0)
        {
            names.Add(KafkaTopicCatalog.ErrorName(family.Topic));
        }

        return names;
    }
}
