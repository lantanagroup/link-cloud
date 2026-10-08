namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

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

public static class ReplicaScalePlanner
{
    public static ReplicaScalePlan Evaluate(string groupId, int currentMembers, int partitionCount, int desiredMembers, int linkMax, bool requireSecondApprover)
    {
        var ceiling = partitionCount;
        if (linkMax > 0)
            ceiling = Math.Min(ceiling, linkMax);

        var plan = new ReplicaScalePlan
        {
            GroupId = groupId,
            CurrentMembers = currentMembers,
            DesiredMembers = desiredMembers,
            PartitionCount = partitionCount,
            Ceiling = ceiling,
            SecondApproverRequired = requireSecondApprover
        };
        plan.Notes.Add("Replica changes are reversible. A replica above the partition count is assigned no partitions and sits idle.");
        plan.Notes.Add($"The ceiling for {groupId} is {ceiling} (maxReplicas cannot exceed the partition count).");

        if (string.IsNullOrWhiteSpace(groupId))
            plan.Errors.Add("A consumer group is required.");
        if (desiredMembers < 0)
            plan.Errors.Add("The replica count cannot be negative.");
        if (desiredMembers == currentMembers)
            plan.Errors.Add("The requested replica count matches the current member count.");
        if (partitionCount <= 0)
            plan.Errors.Add("The group's topics have no partitions, so the replica ceiling cannot be checked.");
        else if (desiredMembers > ceiling)
            plan.Errors.Add($"The replica count {desiredMembers} is above the partition ceiling {ceiling}. Extra replicas sit idle and are refused.");

        plan.Accepted = plan.Errors.Count == 0;
        plan.Summary = plan.Accepted ? string.Join(" ", plan.Notes) : string.Join(" ", plan.Errors) + " " + string.Join(" ", plan.Notes);
        return plan;
    }
}
