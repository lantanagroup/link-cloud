namespace Link.UI.Services;

/// <summary>
/// Which buttons a Kafka change request shows, and what the Execute hold confirmation says.
/// Approve shows only while a second approval is still owed; Execute shows only once the request may run.
/// When two-person approval is on and the requester is not a signed-in person, neither shows: the backend would refuse
/// both, because a second approval from the same shared identity cannot be told apart from the first.
/// </summary>
public static class KafkaChangeActions
{
    public const string UnidentifiedNote =
        "Two-person approval is on, and this request was opened without a signed-in name. Approval and execution need the requester and the approver signed in under their own names, so they stay off until sign-in is turned on.";

    public static bool Identified(string? who) =>
        !string.IsNullOrWhiteSpace(who) && !string.Equals(who.Trim(), "anonymous", StringComparison.OrdinalIgnoreCase);

    public static bool ApprovalBlocked(ChangeRequestRecord request) =>
        request.SecondApproverRequired && !request.SelfApprovalAllowed && !Identified(request.Requester)
        && request.Status is "Pending" or "Approved";

    public static bool CanApprove(ChangeRequestRecord request) =>
        request.Status == "Pending" && request.SecondApproverRequired && !ApprovalBlocked(request);

    public static bool CanExecute(ChangeRequestRecord request) =>
        !ApprovalBlocked(request)
        && (request.Status == "Approved" || (request.Status == "Pending" && !request.SecondApproverRequired));

    /// <summary>Reject closes a request that has not started. Once it runs, only Cancel (for reassignments) applies.</summary>
    public static bool CanReject(ChangeRequestRecord request) => request.Status is "Pending" or "Approved";

    public static string ExecuteHold(ChangeRequestRecord request)
    {
        var (title, action, risks) = Describe(request);
        var details = new List<string[]> { Hold.Pair("Request", request.Kind) };
        if (!string.IsNullOrWhiteSpace(request.Topic))
            details.Add(Hold.Pair("Topic", request.Topic));
        if (!string.IsNullOrWhiteSpace(request.GroupId))
            details.Add(Hold.Pair("Group", request.GroupId));
        if (request.BrokerId >= 0)
            details.Add(Hold.Pair("Broker", request.BrokerId));
        if (Identified(request.Requester))
            details.Add(Hold.Pair("Requested by", request.Requester));
        if (Identified(request.Approver))
            details.Add(Hold.Pair("Approved by", request.Approver));
        if (!string.IsNullOrWhiteSpace(request.Reason))
            details.Add(Hold.Pair("Reason", request.Reason));
        return Hold.Of(title, action, details, [], risks, "", "Hold to execute");
    }

    private static (string Title, string Action, string[] Risks) Describe(ChangeRequestRecord request) => request.Kind switch
    {
        "PartitionIncrease" => ("Add partitions to " + request.Topic,
            "Raise " + request.Topic + " from " + request.BeforePartitions + " to " + request.RequestedPartitions + " partitions, with its retry and error topics.",
            ["Kafka cannot remove partitions. This cannot be reversed.", "Keys can land on a different partition from now on, so per-key order across the change is not kept.", "Consumer groups rebalance once."]),
        "CompleteTopicFamily" => ("Complete the topic family for " + request.Topic,
            "Raise the retry and error topics of " + request.Topic + " to " + request.BeforePartitions + " partitions.",
            ["Kafka cannot remove partitions. This cannot be reversed.", "Consumers of the retry and error topics rebalance once."]),
        "ScaleReplicas" => ("Scale group " + request.GroupId,
            "Change group " + request.GroupId + " from " + request.BeforeReplicas + " to " + request.DesiredReplicas + " members.",
            ["The group rebalances, and reads pause briefly while partitions are reassigned.", "Members above the partition count stay idle."]),
        "AddBroker" => ("Add a broker",
            "Start one more broker and wait until it registers.",
            ["A new broker uses capacity as soon as it starts.", "No partitions move until a rebalance onto it is executed."]),
        "DecommissionBroker" => ("Decommission broker " + request.BrokerId,
            "Move every replica and leader off broker " + request.BrokerId + ", then stop it.",
            ["Partition moves add network and disk load on every broker until they finish.", "Cluster capacity drops once the broker stops.", "A cluster with too few brokers can go under-replicated."]),
        "Rebalance" => ("Rebalance onto broker " + request.BrokerId,
            "Move partitions onto broker " + request.BrokerId + ".",
            ["Partition moves add network and disk load until they finish.", "Leaders can change while partitions move."]),
        "CancelReassignment" => ("Cancel the reassignment",
            "Stop the in-flight reassignment and return replicas to their original brokers.",
            ["Partitions that already moved are moved back, which adds load again.", "The original change has to be requested again."]),
        "ReplicationFactor" => ("Change the replication factor of " + request.Topic,
            "Change " + request.Topic + " from replication factor " + request.BeforeReplicationFactor + " to " + request.TargetReplicationFactor + " under a throttle.",
            ["Copying replicas adds network and disk load until every replica is in the ISR.", "The throttle stays on until the copy finishes."]),
        "Produce" => ("Produce a message to " + request.Topic,
            "Write one message to " + request.Topic + ".",
            ["Consumers of the topic process it like any other message.", "A produced message cannot be taken back."]),
        _ => ("Execute " + request.Kind, "Run this change request.", ["This changes the live cluster."])
    };
}
