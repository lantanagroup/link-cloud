using System.Text.Json;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

/// <summary>
/// Plans a replication-factor change as a partition reassignment.
/// The leader stays. New replicas prefer a free rack and a quiet broker.
/// Unsafe plans (too few brokers, below min.insync.replicas, offline brokers) are refused whole.
/// </summary>
public static class ReplicationFactorPlanner
{
    public const long DefaultThrottle = 10_485_760;
    public const long MaxThrottle = 1_073_741_824;
    public const int HottestCap = 8;

    public static ReplicationFactorEvaluation Evaluate(
        string topic,
        int target,
        long throttle,
        int minInSyncReplicas,
        bool minIsrConfigured,
        IReadOnlyList<BrokerSnapshot> brokers,
        IReadOnlyList<PartitionPlacement> placements,
        string? search,
        int page,
        int pageSize)
    {
        var plan = new ReplicationFactorPlan
        {
            Topic = topic ?? "",
            TargetFactor = target,
            MinInSyncReplicas = minInSyncReplicas < 1 ? 1 : minInSyncReplicas,
            ThrottleBytesPerSecond = throttle,
            PageSize = pageSize is 10 or 25 or 50 ? pageSize : 25,
            Search = Clip(search)
        };
        plan.Notes.Add("The leader stays on each partition. Added replicas prefer a rack that partition does not already use, then the least loaded online broker.");
        plan.Notes.Add("Removed replicas leave the busiest broker first. The leader is not removed.");
        plan.Notes.Add("The request sets a replication throttle, submits the reassignment, waits until every replica is in the ISR, and then clears the throttle.");
        plan.Notes.Add("Nothing moves until the request is submitted.");
        if (!minIsrConfigured)
            plan.Notes.Add("min.insync.replicas is not set on this topic, so 1 is used.");

        var online = (brokers ?? [])
            .Where(broker => string.Equals(broker.State, "up", StringComparison.OrdinalIgnoreCase))
            .GroupBy(broker => broker.Id)
            .Select(group => group.First())
            .OrderBy(broker => broker.Id)
            .ToList();
        plan.OnlineBrokers = online.Count;
        var onlineIds = online.Select(broker => broker.Id).ToHashSet();
        var topicPlacements = (placements ?? [])
            .Where(row => string.Equals(row.Topic, plan.Topic, StringComparison.Ordinal))
            .GroupBy(row => row.Partition)
            .Select(group => group.First())
            .OrderBy(row => row.Partition)
            .ToList();
        plan.PartitionCount = topicPlacements.Count;
        if (topicPlacements.Count > 0)
        {
            var counts = topicPlacements.Select(row => row.Replicas.Distinct().Count()).ToList();
            plan.CurrentFactor = counts.Min();
            plan.Uneven = counts.Max() != counts.Min();
            if (plan.Uneven)
                plan.Notes.Add("Partitions are not all at the same replication factor. The plan brings each one to " + target + ".");
        }

        if (plan.Topic.Length == 0)
            plan.Errors.Add("A topic name is required.");
        if (plan.PartitionCount == 0)
            plan.Errors.Add("Placement for this topic is not in the cluster metadata.");
        if (target < 1)
            plan.Errors.Add("Replication factor must be at least 1.");
        else if (target > 64)
            plan.Errors.Add("Replication factor cannot be higher than 64.");
        else if (online.Count == 0)
            plan.Errors.Add("No broker is online.");
        else if (target > online.Count)
            plan.Errors.Add("Replication factor " + target + " needs " + target + " online brokers. " + online.Count + " are online.");
        if (target >= 1 && target < plan.MinInSyncReplicas)
            plan.Errors.Add("Replication factor " + target + " is below min.insync.replicas (" + plan.MinInSyncReplicas + "). Raise the target, or lower min.insync.replicas first.");
        if (throttle <= 0)
            plan.Errors.Add("A replication throttle, in bytes per second, is required. " + ThrottleText(DefaultThrottle) + " is the usual limit.");
        else if (throttle > MaxThrottle)
            plan.Errors.Add("The throttle cannot be higher than " + ThrottleText(MaxThrottle) + ".");

        if (plan.Errors.Count > 0)
            return Finish(plan, [], [], page);

        var blocked = new List<string>();
        foreach (var placement in topicPlacements)
        {
            var replicas = placement.Replicas.Distinct().ToList();
            if (placement.Leader < 0 || !replicas.Contains(placement.Leader))
                blocked.Add("Partition " + placement.Partition + " has no leader among its replicas.");
            foreach (var id in replicas)
            {
                if (!onlineIds.Contains(id))
                    blocked.Add("Broker " + id + " is offline and holds a replica of partition " + placement.Partition + ".");
            }
        }

        if (blocked.Count > 0)
        {
            plan.Errors.Add("Replicas sit on an offline broker, or a partition has no online leader. The replication factor stays as it is until that is fixed.");
            plan.Errors.AddRange(blocked.Take(HottestCap));
            if (blocked.Count > HottestCap)
                plan.Errors.Add((blocked.Count - HottestCap) + " more partitions are waiting on an offline broker or a missing leader.");
            return Finish(plan, [], [], page);
        }

        var load = online.ToDictionary(broker => broker.Id, broker => broker.PartitionCount);
        var leaders = online.ToDictionary(broker => broker.Id, _ => 0);
        var assignments = new List<ReplicationFactorRow>();
        foreach (var placement in topicPlacements)
        {
            if (leaders.ContainsKey(placement.Leader))
                leaders[placement.Leader]++;
            assignments.Add(Assign(placement, target, online, load));
        }

        plan.ChangedCount = assignments.Count(row => row.Added.Count > 0 || row.Removed.Count > 0);
        plan.AddedReplicas = assignments.Sum(row => row.Added.Count);
        plan.RemovedReplicas = assignments.Sum(row => row.Removed.Count);
        plan.RackProblems = assignments.Count(row => row.RackShared);
        var fair = (int)Math.Ceiling(assignments.Count * (double)target / online.Count);
        plan.FairShare = fair;
        var afterCounts = assignments
            .SelectMany(row => row.After)
            .GroupBy(id => id)
            .ToDictionary(group => group.Key, group => group.Count());
        foreach (var broker in online)
        {
            var after = afterCounts.GetValueOrDefault(broker.Id);
            var ceiling = Math.Max(fair, leaders.GetValueOrDefault(broker.Id));
            if (after > ceiling)
                plan.Errors.Add("Broker " + broker.Id + " would hold " + after + " replicas of this topic. Its ceiling is " + ceiling + " (a fair share is " + fair + ", and it leads " + leaders.GetValueOrDefault(broker.Id) + " partitions).");
        }

        var rackCount = online.Select(RackOf).Distinct(StringComparer.Ordinal).Count();
        var expectedSpread = Math.Min(target, rackCount);
        foreach (var row in assignments)
        {
            var spread = row.Racks.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).Count();
            if (spread < expectedSpread)
                plan.Errors.Add("Partition " + row.Partition + " would keep replicas on " + spread + " rack(s) while " + expectedSpread + " are available.");
            if (plan.Errors.Count > 12)
                break;
        }

        if (plan.ChangedCount == 0)
            plan.Errors.Add("Every partition is already at replication factor " + target + ".");

        var brokerRows = online.Select(broker => new ReplicationFactorBroker
        {
            Id = broker.Id,
            Rack = RackOf(broker),
            State = broker.State,
            Before = assignments.Count(row => row.Before.Contains(broker.Id)),
            After = afterCounts.GetValueOrDefault(broker.Id),
            Leaders = leaders.GetValueOrDefault(broker.Id)
        }).ToList();

        return Finish(plan, assignments, brokerRows, page);
    }

    public static IReadOnlyList<string> CompletedSteps(long throttle, int partitions) =>
    [
        "Throttle set to " + ThrottleText(throttle) + ".",
        "Reassignment submitted for " + partitions + " partitions.",
        "Every replica is in the ISR.",
        "Throttle cleared."
    ];

    public static string ThrottleText(long bytes)
    {
        if (bytes > 0 && bytes % 1_048_576 == 0)
            return (bytes / 1_048_576) + " MiB/s";
        if (bytes > 0 && bytes % 1024 == 0)
            return (bytes / 1024) + " KiB/s";
        return bytes + " bytes/s";
    }

    public static string List(IReadOnlyList<int> ids) =>
        ids.Count == 0 ? "—" : string.Join(", ", ids);

    public static string AssignmentJson(string topic, IReadOnlyList<ReplicationFactorRow> rows)
    {
        var partitions = rows
            .Where(row => row.Added.Count > 0 || row.Removed.Count > 0)
            .Select(row => new { topic, partition = row.Partition, replicas = row.After });
        return JsonSerializer.Serialize(new { version = 1, partitions });
    }

    private static ReplicationFactorRow Assign(PartitionPlacement placement, int target, IReadOnlyList<BrokerSnapshot> online, Dictionary<int, int> load)
    {
        var before = placement.Replicas.Distinct().ToList();
        var leader = placement.Leader;
        var isr = placement.Isr.ToHashSet();
        var after = new List<int> { leader };
        if (target < before.Count)
        {
            var pool = before.Where(id => id != leader).ToList();
            while (after.Count < target && pool.Count > 0)
            {
                var next = pool
                    .OrderBy(id => SharesRack(after, id, online) ? 1 : 0)
                    .ThenByDescending(id => isr.Contains(id))
                    .ThenBy(id => load.GetValueOrDefault(id))
                    .ThenBy(id => id)
                    .First();
                after.Add(next);
                pool.Remove(next);
            }

            foreach (var removed in before.Where(id => !after.Contains(id)))
                load[removed] = load.GetValueOrDefault(removed) - 1;
        }
        else
        {
            foreach (var id in before.Where(id => id != leader))
                after.Add(id);
            while (after.Count < target)
            {
                var pick = online
                    .Where(broker => !after.Contains(broker.Id))
                    .OrderBy(broker => SharesRack(after, broker.Id, online) ? 1 : 0)
                    .ThenBy(broker => load.GetValueOrDefault(broker.Id))
                    .ThenBy(broker => broker.Id)
                    .FirstOrDefault();
                if (pick is null)
                    break;
                after.Add(pick.Id);
                load[pick.Id] = load.GetValueOrDefault(pick.Id) + 1;
            }
        }

        var racks = after.Select(id => RackOf(online.First(broker => broker.Id == id))).Distinct(StringComparer.Ordinal).ToList();
        return new ReplicationFactorRow
        {
            Partition = placement.Partition,
            Leader = leader,
            Before = before,
            After = after,
            Added = after.Except(before).OrderBy(id => id).ToList(),
            Removed = before.Except(after).OrderBy(id => id).ToList(),
            RackShared = racks.Count < after.Count,
            Racks = string.Join(", ", racks)
        };
    }

    private static ReplicationFactorEvaluation Finish(
        ReplicationFactorPlan plan,
        List<ReplicationFactorRow> assignments,
        List<ReplicationFactorBroker> brokers,
        int page)
    {
        plan.Accepted = plan.Errors.Count == 0;
        var orderedBrokers = brokers
            .OrderByDescending(row => row.After)
            .ThenByDescending(row => row.After - row.Before)
            .ThenBy(row => row.Id)
            .ToList();
        plan.BrokerCount = orderedBrokers.Count;
        plan.Hottest = orderedBrokers.Take(HottestCap).ToList();
        plan.Skew = orderedBrokers.Count == 0 && plan.Errors.Count > 0 ? "Not planned." : Skew(orderedBrokers);
        var matched = assignments.Where(row => Match(row, plan.Search)).ToList();
        if (!plan.Accepted && plan.ChangedCount == 0)
            matched = [];
        var worst = matched
            .OrderByDescending(row => row.RackShared)
            .ThenByDescending(row => Math.Abs(row.After.Count - row.Before.Count))
            .ThenByDescending(row => row.Added.Count + row.Removed.Count)
            .ThenBy(row => row.Partition)
            .ToList();
        plan.MatchCount = worst.Count;
        plan.Pages = Math.Max(1, (int)Math.Ceiling(worst.Count / (double)plan.PageSize));
        plan.Page = page < 1 ? 1 : page;
        if (plan.Page > plan.Pages)
            plan.Page = plan.Pages;
        plan.Rows = worst.Skip((plan.Page - 1) * plan.PageSize).Take(plan.PageSize).ToList();
        plan.Summary = plan.Accepted
            ? "Replication factor " + plan.CurrentFactor + " to " + plan.TargetFactor + " on " + plan.Topic + ". " + plan.ChangedCount + " of " + plan.PartitionCount + " partitions change (" + plan.AddedReplicas + " added, " + plan.RemovedReplicas + " removed). Throttle " + ThrottleText(plan.ThrottleBytesPerSecond) + " until every new replica is in the ISR."
            : plan.Errors[0];
        return new ReplicationFactorEvaluation { Plan = plan, Assignments = assignments };
    }

    private static string Skew(IReadOnlyList<ReplicationFactorBroker> brokers)
    {
        if (brokers.Count == 0)
            return "No broker would hold a replica.";
        var busy = brokers[0];
        var quiet = brokers.OrderBy(row => row.After).ThenBy(row => row.Id).First();
        if (busy.After == quiet.After)
            return "Each broker would hold " + busy.After + " replicas.";
        return "Broker " + busy.Id + " would hold " + busy.After + " replicas. Broker " + quiet.Id + " would hold " + quiet.After + " replicas.";
    }

    private static bool Match(ReplicationFactorRow row, string search)
    {
        if (search.Length == 0)
            return true;
        if (row.Partition.ToString().Contains(search, StringComparison.Ordinal))
            return true;
        if (row.Before.Concat(row.After).Any(id => id.ToString().Contains(search, StringComparison.Ordinal)))
            return true;
        if (row.Racks.Contains(search, StringComparison.OrdinalIgnoreCase))
            return true;
        if (search.Equals("add", StringComparison.OrdinalIgnoreCase) && row.Added.Count > 0)
            return true;
        if ((search.Equals("remove", StringComparison.OrdinalIgnoreCase) || search.Equals("drop", StringComparison.OrdinalIgnoreCase)) && row.Removed.Count > 0)
            return true;
        return false;
    }

    private static bool SharesRack(IReadOnlyList<int> chosen, int brokerId, IReadOnlyList<BrokerSnapshot> online)
    {
        var rack = RackOf(online.First(broker => broker.Id == brokerId));
        return chosen.Any(id => string.Equals(RackOf(online.First(broker => broker.Id == id)), rack, StringComparison.Ordinal));
    }

    private static string RackOf(BrokerSnapshot broker) =>
        string.IsNullOrWhiteSpace(broker.Rack) ? "broker-" + broker.Id : broker.Rack.Trim();

    private static string Clip(string? search)
    {
        var text = (search ?? "").Trim();
        return text.Length <= 40 ? text : text[..40];
    }
}

public sealed class ReplicationFactorEvaluation
{
    public ReplicationFactorPlan Plan { get; init; } = new();
    public List<ReplicationFactorRow> Assignments { get; init; } = [];
}

public sealed class ReplicationFactorPlan
{
    public bool Accepted { get; set; }
    public string Topic { get; set; } = "";
    public int CurrentFactor { get; set; }
    public int TargetFactor { get; set; }
    public bool Uneven { get; set; }
    public int MinInSyncReplicas { get; set; } = 1;
    public long ThrottleBytesPerSecond { get; set; }
    public int OnlineBrokers { get; set; }
    public int PartitionCount { get; set; }
    public int ChangedCount { get; set; }
    public int AddedReplicas { get; set; }
    public int RemovedReplicas { get; set; }
    public int RackProblems { get; set; }
    public int FairShare { get; set; }
    public int MatchCount { get; set; }
    public int Page { get; set; } = 1;
    public int Pages { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string Search { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Skew { get; set; } = "";
    public List<string> Errors { get; set; } = [];
    public List<string> Notes { get; set; } = [];
    public List<ReplicationFactorRow> Rows { get; set; } = [];
    public List<ReplicationFactorBroker> Hottest { get; set; } = [];
    public int BrokerCount { get; set; }
}

public sealed class ReplicationFactorRow
{
    public int Partition { get; set; }
    public int Leader { get; set; }
    public List<int> Before { get; set; } = [];
    public List<int> After { get; set; } = [];
    public List<int> Added { get; set; } = [];
    public List<int> Removed { get; set; } = [];
    public bool RackShared { get; set; }
    public string Racks { get; set; } = "";
}

public sealed class ReplicationFactorBroker
{
    public int Id { get; set; }
    public string Rack { get; set; } = "";
    public string State { get; set; } = "";
    public int Before { get; set; }
    public int After { get; set; }
    public int Leaders { get; set; }
}
