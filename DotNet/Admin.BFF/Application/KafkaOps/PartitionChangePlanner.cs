using System.Text.Json.Serialization;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public sealed class GroupSafetySnapshot
{
    public string GroupId { get; set; } = "";
    public int MemberCount { get; set; }
    public int MembersOnExpectedConfig { get; set; }
    public long TotalLag { get; set; }
}

public sealed record PartitionPlanRequest
{
    public string Topic { get; set; } = "";
    public int CurrentPartitions { get; set; }
    public int RetryPartitions { get; set; } = -1;
    public bool RetryTopicExists { get; set; }
    public int ErrorPartitions { get; set; } = -1;
    public bool ErrorTopicExists { get; set; }
    public int RequestedPartitions { get; set; }
    public int Cap { get; set; }
    public bool QuietWindowMet { get; set; }
    public string QuietWindowDetail { get; set; } = "";
    public bool OverrideQuietWindow { get; set; }
    public string? OverrideReason { get; set; }
    public bool RequireSecondApprover { get; set; }
    public bool EnvironmentChangeInFlight { get; set; }
    public bool FamilyChangeInFlight { get; set; }
    public DateTimeOffset? LastFamilyChangeUtc { get; set; }
    public int RateLimitMinutes { get; set; } = 30;
    public DateTimeOffset Now { get; set; }
    public int ExpectedConfigVersion { get; set; } = 1;
    public int MetadataRefreshIntervalMs { get; set; } = 30_000;
    public List<GroupSafetySnapshot> Groups { get; set; } = [];
}

public sealed class PartitionPlan
{
    public bool Accepted { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Notes { get; set; } = [];
    public string Topic { get; set; } = "";
    public string Family { get; set; } = "";
    public string RetryTopic { get; set; } = "";
    public string ErrorTopic { get; set; } = "";
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public KafkaTopicKeyClass KeyClass { get; set; }
    public int CurrentPartitions { get; set; }
    public int RequestedPartitions { get; set; }
    public int MaxReplicas { get; set; }
    public bool SecondApproverRequired { get; set; }
    public bool QuietWindowRequired { get; set; }
    public bool QuietWindowMet { get; set; }
    public bool QuietWindowOverride { get; set; }
    public bool Irreversible { get; set; } = true;
    public List<string> AffectedGroups { get; set; } = [];
    public string KeyShape { get; set; } = "";
    public bool HardBlocked { get; set; }
    public string Summary { get; set; } = "";
}

public static class PartitionChangePlanner
{
    public static PartitionPlan Evaluate(PartitionPlanRequest request)
    {
        var family = KafkaTopicCatalog.MainName(request.Topic);
        var entry = KafkaTopicCatalog.Find(family);
        var keyClass = entry?.KeyClass ?? KafkaTopicKeyClass.Facility;
        var hardBlocked = entry?.HardBlocked == true;
        var quietRequired = entry is null || entry.OrderSensitive;
        var plan = new PartitionPlan
        {
            Topic = family,
            Family = family,
            RetryTopic = KafkaTopicCatalog.RetryName(family),
            ErrorTopic = KafkaTopicCatalog.ErrorName(family),
            KeyClass = keyClass,
            KeyShape = entry?.KeyShape ?? "{facilityId}",
            HardBlocked = hardBlocked,
            CurrentPartitions = request.CurrentPartitions,
            RequestedPartitions = request.RequestedPartitions,
            MaxReplicas = request.RequestedPartitions,
            QuietWindowRequired = quietRequired,
            QuietWindowMet = request.QuietWindowMet,
            QuietWindowOverride = request.OverrideQuietWindow,
            AffectedGroups = request.Groups.Select(group => group.GroupId).Where(id => id.Length > 0).Distinct(StringComparer.Ordinal).ToList()
        };

        plan.Notes.Add("Adding partitions cannot be reversed. Keys are remapped, and Kafka does not shrink a topic.");
        plan.Notes.Add("The main topic, the retry topic, and the error topic are raised together.");
        if (keyClass == KafkaTopicKeyClass.Facility)
            plan.Notes.Add("A single facility can never use more than one partition of a facility-keyed topic. Extra partitions help only when many facilities are active at once.");
        if (keyClass == KafkaTopicKeyClass.Patient)
            plan.Notes.Add("A patient key is {facilityId}:{patientId}. Raising the partition count remaps which partition that patient uses.");
        if (keyClass == KafkaTopicKeyClass.Report)
            plan.Notes.Add("A report key is {facilityId}:{reportScheduleId}. Raising the partition count remaps that report.");

        if (hardBlocked)
            plan.Errors.Add(entry!.BlockReason);

        if (string.IsNullOrWhiteSpace(family) || KafkaTopicCatalog.IsRetry(request.Topic) || KafkaTopicCatalog.IsError(request.Topic))
            plan.Errors.Add("Choose a main topic. Retry and error topics follow the main topic and are not increased on their own.");

        if (request.RequestedPartitions <= request.CurrentPartitions)
            plan.Errors.Add("The new partition count must be higher than the current count.");

        if (request.Cap > 0 && request.RequestedPartitions > request.Cap)
            plan.Errors.Add($"The new partition count must be at most {request.Cap} for this environment.");

        if (request.CurrentPartitions <= 0)
            plan.Errors.Add("The topic was not found on the broker.");

        if (!request.RetryTopicExists)
            plan.Errors.Add("The retry topic is not on the broker, so the family cannot be increased together.");
        else if (request.RetryPartitions > request.RequestedPartitions)
            plan.Errors.Add("The retry topic already has more partitions than the requested count. The change would shrink it, which is refused.");
        else if (request.RetryPartitions < request.RequestedPartitions)
            plan.Notes.Add($"The retry topic will be raised from {request.RetryPartitions} to {request.RequestedPartitions}.");
        else
            plan.Notes.Add("The retry topic is already at the requested count.");

        if (!request.ErrorTopicExists)
            plan.Errors.Add("The error topic is not on the broker, so the family cannot be increased together.");
        else if (request.ErrorPartitions > request.RequestedPartitions)
            plan.Errors.Add("The error topic already has more partitions than the requested count. The change would shrink it, which is refused.");
        else if (request.ErrorPartitions < request.RequestedPartitions)
            plan.Notes.Add($"The error topic will be raised from {request.ErrorPartitions} to {request.RequestedPartitions}.");
        else
            plan.Notes.Add("The error topic is already at the requested count.");

        foreach (var group in request.Groups)
        {
            if (group.MemberCount == 0)
            {
                plan.Notes.Add($"Group {group.GroupId} has no members, so there is no running consumer to check.");
                continue;
            }

            if (group.MembersOnExpectedConfig < group.MemberCount)
            {
                plan.Errors.Add(
                    $"Group {group.GroupId} has {group.MemberCount - group.MembersOnExpectedConfig} member(s) that do not advertise consumer config c{request.ExpectedConfigVersion} (earliest offset reset and fast metadata refresh).");
            }
        }

        if (quietRequired && !hardBlocked && !request.QuietWindowMet && !request.OverrideQuietWindow)
            plan.Errors.Add("This topic keeps per-key order. Every subscribed group must have zero lag, and the topic must show no produce traffic for at least twice the metadata refresh, before the change can run. " + request.QuietWindowDetail);

        if (request.OverrideQuietWindow)
        {
            if (!quietRequired)
                plan.Notes.Add("A quiet-window override was set, but this key class does not require a quiet window.");
            else if (string.IsNullOrWhiteSpace(request.OverrideReason))
                plan.Errors.Add("A quiet-window override requires a reason.");
            else
                plan.Notes.Add("Quiet-window override requested: " + request.OverrideReason.Trim());
        }

        plan.SecondApproverRequired = !hardBlocked && (quietRequired || request.OverrideQuietWindow || request.RequireSecondApprover);
        if (plan.SecondApproverRequired)
            plan.Notes.Add("A different person must approve this request before it can run.");

        if (request.EnvironmentChangeInFlight)
            plan.Errors.Add("Another partition change is already in flight in this environment.");

        if (request.FamilyChangeInFlight)
            plan.Errors.Add("This topic family already has a partition change in flight.");

        if (request.LastFamilyChangeUtc is { } last
            && request.Now - last < TimeSpan.FromMinutes(Math.Max(1, request.RateLimitMinutes)))
        {
            plan.Errors.Add($"This topic family was changed at {last:yyyy-MM-dd HH:mm:ss}Z. The next change must wait {request.RateLimitMinutes} minutes.");
        }

        plan.MaxReplicas = request.RequestedPartitions;
        plan.Notes.Add($"After this change, maxReplicas for each subscribed group is {plan.MaxReplicas} (it cannot exceed the partition count).");
        plan.Accepted = plan.Errors.Count == 0;
        plan.Summary = string.Join(" ", plan.Notes);
        if (!plan.Accepted)
            plan.Summary = string.Join(" ", plan.Errors) + " " + plan.Summary;
        return plan;
    }

    public static bool MemberIsSafe(string? clientId, int expectedVersion) =>
        KafkaConfigAdvertisement.ClientAdvertisesExpectedConfig(clientId, expectedVersion);
}
