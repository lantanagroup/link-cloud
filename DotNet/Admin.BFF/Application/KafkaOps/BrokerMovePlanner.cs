namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public sealed class BrokerPartitionFact
{
    public string Topic { get; set; } = "";
    public int Partition { get; set; }
    public int Leader { get; set; }
    public List<int> Replicas { get; set; } = [];
    public int MinInSyncReplicas { get; set; } = 1;
}

public sealed class ReplicaMove
{
    public string Topic { get; set; } = "";
    public int Partition { get; set; }
    public int FromBroker { get; set; }
    public int ToBroker { get; set; }
    public List<int> Replicas { get; set; } = [];
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

public static class BrokerMovePlanner
{
    public static BrokerMovePlan Decommission(
        int brokerId,
        IReadOnlyList<int> brokerIds,
        IReadOnlyList<BrokerPartitionFact> partitions,
        IReadOnlyCollection<int>? controllerEligibleIds = null)
    {
        var plan = new BrokerMovePlan { BrokerId = brokerId };
        plan.Notes.Add("Removing a broker moves every replica off it first. The broker is stopped only after it has no replicas, no leaders, and the cluster has no under-replicated or offline partitions.");
        if (!brokerIds.Contains(brokerId))
            plan.Errors.Add($"Broker {brokerId} is not in the cluster.");
        if (controllerEligibleIds is not null && controllerEligibleIds.Contains(brokerId))
            plan.Errors.Add($"Broker {brokerId} is controller-eligible. Decommission a broker that is not in the controller quorum.");

        var others = brokerIds.Where(id => id != brokerId).ToList();
        var load = others.ToDictionary(id => id, _ => 0);
        foreach (var partition in partitions)
        {
            if (!partition.Replicas.Contains(brokerId))
            {
                foreach (var replica in partition.Replicas)
                {
                    if (load.ContainsKey(replica))
                        load[replica]++;
                }
                continue;
            }

            var kept = partition.Replicas.Where(id => id != brokerId).ToList();
            var candidates = others.Where(id => !kept.Contains(id)).OrderBy(id => load.GetValueOrDefault(id)).ThenBy(id => id).ToList();
            if (candidates.Count == 0 || kept.Count + 1 < Math.Max(partition.Replicas.Count, 1))
            {
                plan.Errors.Add($"Topic {partition.Topic} partition {partition.Partition} would have fewer brokers than its replication factor if broker {brokerId} left.");
                continue;
            }

            var destination = candidates[0];
            var next = new List<int>(kept) { destination };
            var minIsr = Math.Max(1, partition.MinInSyncReplicas);
            if (next.Count < minIsr)
            {
                plan.Errors.Add($"Topic {partition.Topic} partition {partition.Partition} would fall below min.insync.replicas ({minIsr}).");
                continue;
            }

            load[destination] = load.GetValueOrDefault(destination) + 1;
            plan.Moves.Add(new ReplicaMove
            {
                Topic = partition.Topic,
                Partition = partition.Partition,
                FromBroker = brokerId,
                ToBroker = destination,
                Replicas = next
            });
            if (partition.Leader == brokerId)
                plan.Notes.Add($"Leader of {partition.Topic}-{partition.Partition} moves off broker {brokerId}.");
        }

        plan.AlreadyEmpty = plan.Moves.Count == 0 && plan.Errors.Count == 0;
        if (plan.AlreadyEmpty)
            plan.Notes.Add($"Broker {brokerId} has no replicas. It can be stopped once the cluster is green.");
        else
            plan.Notes.Add($"{plan.Moves.Count} partition(s) move off broker {brokerId}.");

        plan.Accepted = plan.Errors.Count == 0;
        plan.Summary = plan.Accepted ? string.Join(" ", plan.Notes) : string.Join(" ", plan.Errors);
        return plan;
    }

    public static BrokerMovePlan SpreadOnto(int brokerId, IReadOnlyList<int> brokerIds, IReadOnlyList<BrokerPartitionFact> partitions)
    {
        var plan = new BrokerMovePlan { BrokerId = brokerId, SecondApproverRequired = true };
        if (!brokerIds.Contains(brokerId))
            plan.Errors.Add($"Broker {brokerId} is not in the cluster yet.");

        var load = brokerIds.ToDictionary(id => id, id => partitions.Count(partition => partition.Replicas.Contains(id)));
        var replicaCount = partitions.Sum(partition => partition.Replicas.Count);
        var average = brokerIds.Count == 0 ? 0 : (replicaCount + brokerIds.Count - 1) / brokerIds.Count;
        foreach (var partition in partitions.OrderBy(item => item.Topic, StringComparer.Ordinal).ThenBy(item => item.Partition))
        {
            if (load.GetValueOrDefault(brokerId) >= average)
                break;
            if (partition.Replicas.Contains(brokerId) || partition.Replicas.Count == 0)
                continue;

            var ranked = partition.Replicas
                .Where(id => id != brokerId)
                .OrderByDescending(id => load.GetValueOrDefault(id))
                .ThenBy(id => id)
                .ToList();
            if (ranked.Count == 0)
                continue;
            var source = ranked[0];

            var next = partition.Replicas.Where(id => id != source).Append(brokerId).ToList();
            if (next.Count < Math.Max(1, partition.MinInSyncReplicas))
            {
                plan.Errors.Add($"Moving {partition.Topic}-{partition.Partition} onto broker {brokerId} would fall below min.insync.replicas.");
                continue;
            }

            load[source] = load.GetValueOrDefault(source) - 1;
            load[brokerId] = load.GetValueOrDefault(brokerId) + 1;
            plan.Moves.Add(new ReplicaMove
            {
                Topic = partition.Topic,
                Partition = partition.Partition,
                FromBroker = source,
                ToBroker = brokerId,
                Replicas = next
            });
        }

        plan.Notes.Add(plan.Moves.Count == 0
            ? $"Broker {brokerId} already holds its share of replicas."
            : $"{plan.Moves.Count} partition(s) move onto broker {brokerId}.");
        plan.Accepted = plan.Errors.Count == 0;
        plan.Summary = plan.Accepted ? string.Join(" ", plan.Notes) : string.Join(" ", plan.Errors);
        return plan;
    }
}
