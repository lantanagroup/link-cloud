using System.Text.Json.Serialization;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum KafkaChangeStatus
{
    Pending,
    Approved,
    Rejected,
    Executing,
    Converging,
    Verifying,
    Done,
    NeedsAttention,
    Failed,
    Cancelled,
    TimedOut
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum KafkaChangeKind
{
    PartitionIncrease,
    CompleteTopicFamily,
    ScaleReplicas,
    AddBroker,
    DecommissionBroker,
    Rebalance,
    CancelReassignment
}

public sealed class GroupProgress
{
    public string GroupId { get; set; } = "";
    public int AssignedPartitions { get; set; }
    public int ExpectedPartitions { get; set; }
    public bool Complete { get; set; }
}

public sealed class ChangeRequestRecord
{
    public Guid Id { get; set; }
    public KafkaChangeKind Kind { get; set; } = KafkaChangeKind.PartitionIncrease;
    public string Environment { get; set; } = "";
    public string GroupId { get; set; } = "";
    public int DesiredReplicas { get; set; }
    public int BeforeReplicas { get; set; }
    public int BrokerId { get; set; } = -1;
    public string ReassignmentJson { get; set; } = "";
    public string OriginalAssignmentJson { get; set; } = "";
    public string RebalanceName { get; set; } = "";
    public Guid TargetRequestId { get; set; }
    public string Warning { get; set; } = "";
    public string Progress { get; set; } = "";
    public string Topic { get; set; } = "";
    public string Family { get; set; } = "";
    public string RetryTopic { get; set; } = "";
    public int BeforePartitions { get; set; }
    public int RequestedPartitions { get; set; }
    public int MaxReplicas { get; set; }
    public string KeyClass { get; set; } = "";
    public bool SecondApproverRequired { get; set; }
    public bool QuietWindowOverride { get; set; }
    public string Reason { get; set; } = "";
    public string OverrideReason { get; set; } = "";
    public string Requester { get; set; } = "";
    public string Approver { get; set; } = "";
    public string DryRunSummary { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public KafkaChangeStatus Status { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? ApprovedUtc { get; set; }
    public DateTimeOffset? ExecutedUtc { get; set; }
    public DateTimeOffset? PartitionsChangedUtc { get; set; }
    public DateTimeOffset? ConvergedUtc { get; set; }
    public DateTimeOffset? ClosedUtc { get; set; }
    public string Failure { get; set; } = "";
    public bool AlertRaised { get; set; }
    public bool BaselineCaptured { get; set; }
    public long BaselineLag { get; set; }
    public long ErrorWatermarkAtStart { get; set; } = -1;
    public bool ErrorTopicAdvanced { get; set; }
    public List<OffsetCheckpoint> BaselineOffsets { get; set; } = [];
    public List<OffsetCheckpoint> LatestOffsets { get; set; } = [];
    public List<string> AffectedGroups { get; set; } = [];
    public List<GroupProgress> Groups { get; set; } = [];
    public int PollFailures { get; set; }
    public DateTimeOffset? NextPollUtc { get; set; }
}

public sealed class OffsetCheckpoint
{
    public string Topic { get; set; } = "";
    public int Partition { get; set; }
    public long Committed { get; set; }
    public long HighWatermark { get; set; }
}

public static class ChangeRequestWorkflow
{
    public static string? Approve(ChangeRequestRecord request, string user, DateTimeOffset now)
    {
        if (request.Status != KafkaChangeStatus.Pending)
            return "Only a pending request can be approved.";
        if (!request.SecondApproverRequired)
            return "This request does not need a separate approver.";
        if (SameUser(request.Requester, user))
            return "The requester cannot approve their own request.";
        if (string.IsNullOrWhiteSpace(user))
            return "An approver identity is required.";

        request.Status = KafkaChangeStatus.Approved;
        request.Approver = user;
        request.ApprovedUtc = now;
        return null;
    }

    public static string? Reject(ChangeRequestRecord request, string user, string reason, DateTimeOffset now)
    {
        if (request.Status is not (KafkaChangeStatus.Pending or KafkaChangeStatus.Approved))
            return "Only a pending or approved request can be rejected.";
        if (string.IsNullOrWhiteSpace(reason))
            return "A rejection reason is required.";

        request.Status = KafkaChangeStatus.Rejected;
        request.Approver = user;
        request.Failure = reason.Trim();
        request.ClosedUtc = now;
        return null;
    }

    public static string? MarkExecuting(ChangeRequestRecord request, string user, DateTimeOffset now)
    {
        if (request.SecondApproverRequired)
        {
            if (request.Status != KafkaChangeStatus.Approved)
                return "This request must be approved by someone else before it can run.";
            if (SameUser(request.Requester, user))
                return "The requester cannot execute their own request.";
        }
        else if (request.Status is not (KafkaChangeStatus.Pending or KafkaChangeStatus.Approved))
        {
            return "This request cannot be executed from its current state.";
        }

        request.Status = KafkaChangeStatus.Executing;
        request.ExecutedUtc = now;
        if (!request.SecondApproverRequired)
            request.Approver = user;
        return null;
    }

    public static bool SameUser(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left)
        && !string.IsNullOrWhiteSpace(right)
        && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
