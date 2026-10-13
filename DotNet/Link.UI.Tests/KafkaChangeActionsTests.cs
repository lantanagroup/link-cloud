using Link.UI.Services;
using System.Text.Json;
using Xunit;

namespace Link.UI.Tests;

public class KafkaChangeActionsTests
{
    private static ChangeRequestRecord Request(string status, bool second, bool self, string requester = "ada@example.org", string kind = "Rebalance") => new()
    {
        Id = Guid.NewGuid(),
        Kind = kind,
        Status = status,
        SecondApproverRequired = second,
        SelfApprovalAllowed = self,
        Requester = requester,
        BrokerId = 2,
        Reason = "spread load"
    };

    [Fact]
    public void Pending_WithASecondApprover_ShowsApprove_NotExecute()
    {
        var request = Request("Pending", second: true, self: false);
        Assert.True(KafkaChangeActions.CanApprove(request));
        Assert.False(KafkaChangeActions.CanExecute(request));
    }

    [Fact]
    public void Approved_ShowsExecute_NotApprove()
    {
        var request = Request("Approved", second: true, self: false);
        Assert.False(KafkaChangeActions.CanApprove(request));
        Assert.True(KafkaChangeActions.CanExecute(request));
    }

    [Fact]
    public void NoSecondApprover_ShowsExecuteOnly()
    {
        var request = Request("Approved", second: false, self: true, "anonymous", "PartitionIncrease");
        Assert.False(KafkaChangeActions.CanApprove(request));
        Assert.True(KafkaChangeActions.CanExecute(request));
        Assert.False(KafkaChangeActions.ApprovalBlocked(request));
    }

    [Fact]
    public void LocalDevelopment_LetsTheAnonymousRequesterApproveThenExecute()
    {
        var pending = Request("Pending", second: true, self: true, "anonymous");
        Assert.True(KafkaChangeActions.CanApprove(pending));
        Assert.False(KafkaChangeActions.ApprovalBlocked(pending));
        Assert.True(KafkaChangeActions.CanExecute(Request("Approved", second: true, self: true, "anonymous")));
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Approved")]
    public void TwoPersonApproval_WithoutSignIn_HidesBothAndExplains(string status)
    {
        var request = Request(status, second: true, self: false, "anonymous");
        Assert.True(KafkaChangeActions.ApprovalBlocked(request));
        Assert.False(KafkaChangeActions.CanApprove(request));
        Assert.False(KafkaChangeActions.CanExecute(request));
        Assert.Contains("signed in", KafkaChangeActions.UnidentifiedNote);
    }

    [Theory]
    [InlineData("Executing")]
    [InlineData("Done")]
    [InlineData("Rejected")]
    public void ClosedOrRunning_ShowsNeither(string status)
    {
        var request = Request(status, second: true, self: true);
        Assert.False(KafkaChangeActions.CanApprove(request));
        Assert.False(KafkaChangeActions.CanExecute(request));
        Assert.False(KafkaChangeActions.CanReject(request));
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Approved")]
    public void NotStarted_CanBeRejected(string status) => Assert.True(KafkaChangeActions.CanReject(Request(status, true, true)));

    [Theory]
    [InlineData("PartitionIncrease", "cannot be reversed")]
    [InlineData("DecommissionBroker", "under-replicated")]
    [InlineData("Rebalance", "network and disk load")]
    [InlineData("ScaleReplicas", "rebalances")]
    [InlineData("CancelReassignment", "moved back")]
    [InlineData("ReplicationFactor", "throttle")]
    [InlineData("AddBroker", "capacity")]
    [InlineData("Produce", "cannot be taken back")]
    public void ExecuteHold_StatesTheActionAndRisks_AndAsksNothingTheRequestKnows(string kind, string risk)
    {
        var request = Request("Approved", true, false, kind: kind);
        request.Topic = "ReportScheduled";
        using var spec = JsonDocument.Parse(KafkaChangeActions.ExecuteHold(request));
        var root = spec.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("action").GetString()));
        Assert.Contains(root.GetProperty("risks").EnumerateArray(), item => item.GetString()!.Contains(risk, StringComparison.Ordinal));
        Assert.Equal(0, root.GetProperty("fields").GetArrayLength());
        Assert.Equal("", root.GetProperty("confirm").GetString());
        Assert.Equal("Hold to execute", root.GetProperty("label").GetString());
    }

    [Fact]
    public void ExecuteHold_LeavesOutTheSharedAnonymousName()
    {
        var request = Request("Approved", true, true, "anonymous");
        Assert.DoesNotContain("anonymous", KafkaChangeActions.ExecuteHold(request), StringComparison.OrdinalIgnoreCase);
    }
}
