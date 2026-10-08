using LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace UnitTests.Kafka;

[Trait("Category", "UnitTests")]
public class PartitionChangePlannerTests
{
    [Fact]
    public void PatientTopic_RequiresQuietWindow_BecauseTheKeyRemaps()
    {
        var quiet = PartitionChangePlanner.Evaluate(Valid("ResourcesAcquired"));
        Assert.True(quiet.Accepted);
        Assert.True(quiet.QuietWindowRequired);
        Assert.Equal("{facilityId}:{patientId}", quiet.KeyShape);
        Assert.Equal(6, quiet.MaxReplicas);
        Assert.Contains("cannot be reversed", quiet.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("raised together", quiet.Summary, StringComparison.OrdinalIgnoreCase);

        var busy = PartitionChangePlanner.Evaluate(Valid("ResourcesAcquired") with { QuietWindowMet = false });
        Assert.False(busy.Accepted);
        Assert.Contains(busy.Errors, error => error.Contains("per-key order", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LogTopic_AllowsIncreaseWithoutQuietWindow()
    {
        var plan = PartitionChangePlanner.Evaluate(Valid("ReadyToAcquire") with { QuietWindowMet = false });

        Assert.True(plan.Accepted);
        Assert.False(plan.QuietWindowRequired);
        Assert.Equal(KafkaTopicKeyClass.Log, plan.KeyClass);
    }

    [Fact]
    public void MixedHashTopic_StaysBlocked()
    {
        var plan = PartitionChangePlanner.Evaluate(Valid("DataAcquisitionRequested") with { QuietWindowMet = true });

        Assert.False(plan.Accepted);
        Assert.True(plan.HardBlocked);
        Assert.Contains(plan.Errors, error => error.Contains("murmur2", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ErrorTopic_MustGrowWithTheMainTopic()
    {
        var missing = PartitionChangePlanner.Evaluate(Valid("ReadyToAcquire") with { ErrorTopicExists = false, QuietWindowMet = false });
        Assert.Contains(missing.Errors, error => error.Contains("error topic is not on the broker", StringComparison.OrdinalIgnoreCase));

        var ahead = PartitionChangePlanner.Evaluate(Valid("ReadyToAcquire") with { ErrorPartitions = 12, QuietWindowMet = false });
        Assert.Contains(ahead.Errors, error => error.Contains("shrink", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RefusesShrink_Cap_MissingTopic_AndRetryAhead()
    {
        var shrink = PartitionChangePlanner.Evaluate(Valid("ResourcesAcquired") with { RequestedPartitions = 3 });
        Assert.Contains(shrink.Errors, error => error.Contains("higher than the current", StringComparison.Ordinal));

        var over = PartitionChangePlanner.Evaluate(Valid("ResourcesAcquired") with { RequestedPartitions = 30 });
        Assert.Contains(over.Errors, error => error.Contains("at most", StringComparison.Ordinal));

        var missing = PartitionChangePlanner.Evaluate(Valid("ResourcesAcquired") with { CurrentPartitions = 0 });
        Assert.Contains(missing.Errors, error => error.Contains("not found", StringComparison.Ordinal));

        var retry = PartitionChangePlanner.Evaluate(Valid("ResourcesAcquired") with { RetryPartitions = 12 });
        Assert.Contains(retry.Errors, error => error.Contains("shrink", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FacilityTopic_RequiresQuietWindow_AndSaysOneFacilityUsesOnePartition()
    {
        var blocked = PartitionChangePlanner.Evaluate(Valid("ReportScheduled") with { QuietWindowMet = false });
        Assert.False(blocked.Accepted);
        Assert.True(blocked.QuietWindowRequired);
        Assert.True(blocked.SecondApproverRequired);
        Assert.Contains(blocked.Notes, note => note.Contains("single facility", StringComparison.OrdinalIgnoreCase));

        var open = PartitionChangePlanner.Evaluate(Valid("ReportScheduled") with { QuietWindowMet = true });
        Assert.True(open.Accepted);
        Assert.True(open.SecondApproverRequired);
    }

    [Fact]
    public void UnknownTopic_IsFacilityKeyed()
    {
        Assert.Equal(KafkaTopicKeyClass.Facility, KafkaTopicCatalog.KeyClassOf("NotARealTopic"));
        var plan = PartitionChangePlanner.Evaluate(Valid("NotARealTopic") with { CurrentPartitions = 3, QuietWindowMet = false });
        Assert.True(plan.QuietWindowRequired);
        Assert.False(plan.Accepted);
    }

    [Fact]
    public void UnsafeMember_Blocks_AndEmptyGroupDoesNot()
    {
        var request = Valid("ResourcesAcquired");
        request.Groups[0].MemberCount = 2;
        request.Groups[0].MembersOnExpectedConfig = 1;
        var blocked = PartitionChangePlanner.Evaluate(request);
        Assert.Contains(blocked.Errors, error => error.Contains("do not advertise", StringComparison.Ordinal));

        request.Groups[0].MemberCount = 0;
        request.Groups[0].MembersOnExpectedConfig = 0;
        var empty = PartitionChangePlanner.Evaluate(request);
        Assert.True(empty.Accepted);
    }

    [Fact]
    public void OverrideWithoutReason_IsRefused_AndSecondApproverIsRequired()
    {
        var plan = PartitionChangePlanner.Evaluate(Valid("ReportScheduled") with
        {
            QuietWindowMet = false,
            OverrideQuietWindow = true,
            OverrideReason = " "
        });

        Assert.False(plan.Accepted);
        Assert.True(plan.SecondApproverRequired);
        Assert.Contains(plan.Errors, error => error.Contains("override requires a reason", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InFlightAndRateLimit_AreRefused()
    {
        var now = DateTimeOffset.Parse("2026-10-07T12:00:00Z");
        var inFlight = PartitionChangePlanner.Evaluate(Valid("ResourcesAcquired") with { EnvironmentChangeInFlight = true, Now = now });
        Assert.Contains(inFlight.Errors, error => error.Contains("in flight", StringComparison.Ordinal));

        var recent = PartitionChangePlanner.Evaluate(Valid("ResourcesAcquired") with
        {
            Now = now,
            LastFamilyChangeUtc = now.AddMinutes(-10)
        });
        Assert.Contains(recent.Errors, error => error.Contains("must wait", StringComparison.Ordinal));
    }

    [Fact]
    public void CompleteFamily_RaisesOnlySiblings_AndSkipsTheRateLimit()
    {
        var uneven = PartitionChangePlanner.CompleteFamily(Valid("ReadyToAcquire") with
        {
            CurrentPartitions = 4,
            RetryPartitions = 3,
            ErrorPartitions = 3,
            QuietWindowMet = false,
            LastFamilyChangeUtc = DateTimeOffset.Parse("2026-10-07T11:50:00Z")
        });

        Assert.True(uneven.Accepted);
        Assert.Equal(4, uneven.RequestedPartitions);
        Assert.Equal(["ReadyToAcquire-Retry", "ReadyToAcquire-Error"], uneven.TopicsToRaise);
        Assert.DoesNotContain("ReadyToAcquire", uneven.TopicsToRaise);
        Assert.Contains(uneven.Notes, note => note.Contains("retry topic will be raised from 3 to 4", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(uneven.Errors, error => error.Contains("must wait", StringComparison.Ordinal));

        var even = PartitionChangePlanner.CompleteFamily(Valid("ReadyToAcquire") with
        {
            CurrentPartitions = 4,
            RetryPartitions = 4,
            ErrorPartitions = 4,
            QuietWindowMet = false
        });
        Assert.False(even.Accepted);
        Assert.Contains(even.Errors, error => error.Contains("No sibling is behind", StringComparison.Ordinal));

        var ahead = PartitionChangePlanner.CompleteFamily(Valid("ReadyToAcquire") with
        {
            CurrentPartitions = 4,
            RetryPartitions = 5,
            ErrorPartitions = 3,
            QuietWindowMet = false
        });
        Assert.True(ahead.Accepted);
        Assert.Equal(["ReadyToAcquire-Error"], ahead.TopicsToRaise);
        Assert.Contains(ahead.Notes, note => note.Contains("not be shrunk", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReassignmentInFlight_RefusesThePartitionPlan()
    {
        var plan = PartitionChangePlanner.Evaluate(Valid("ReadyToAcquire") with
        {
            QuietWindowMet = false,
            InFlightReassignmentTopics = ["ops-proof-log"]
        });

        Assert.False(plan.Accepted);
        Assert.Contains(plan.Errors, error => error.Contains("ops-proof-log", StringComparison.Ordinal));
    }

    private static PartitionPlanRequest Valid(string topic)
    {
        var entry = KafkaTopicCatalog.Find(topic);
        return new PartitionPlanRequest
        {
            Topic = topic,
            CurrentPartitions = 3,
            RetryPartitions = 3,
            RetryTopicExists = true,
            ErrorPartitions = 3,
            ErrorTopicExists = true,
            RequestedPartitions = 6,
            Cap = 24,
            QuietWindowMet = true,
            ExpectedConfigVersion = 1,
            Now = DateTimeOffset.Parse("2026-10-07T12:00:00Z"),
            Groups = (entry?.Groups ?? ["Normalization"]).Select(group => new GroupSafetySnapshot
            {
                GroupId = group,
                MemberCount = 1,
                MembersOnExpectedConfig = 1
            }).ToList()
        };
    }
}
