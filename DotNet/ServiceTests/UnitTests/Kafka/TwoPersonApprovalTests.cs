using System.Diagnostics;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

namespace UnitTests.Kafka;

[Trait("Category", "UnitTests")]
public class TwoPersonApprovalTests
{
    [Fact]
    public void Default_IsOn_AndRequiresASecondDifferentApprover()
    {
        var options = new KafkaOpsOptions();
        Assert.True(options.TwoPersonApproval);
        Assert.True(options.SecondApprover);
        Assert.True(options.DifferentApprover);
    }

    [Fact]
    public void Off_LetsTheRequesterApproveAndExecute()
    {
        var options = new KafkaOpsOptions { TwoPersonApproval = false, RequireDifferentApprover = false };
        Assert.False(options.SecondApprover);
        Assert.False(options.DifferentApprover);

        var request = Pending("anonymous");
        Assert.Null(ChangeRequestWorkflow.Approve(request, "anonymous", DateTimeOffset.UnixEpoch, options.DifferentApprover));
        Assert.Null(ChangeRequestWorkflow.MarkExecuting(request, "anonymous", DateTimeOffset.UnixEpoch, options.DifferentApprover));
        Assert.Equal(KafkaChangeStatus.Executing, request.Status);
    }

    [Fact]
    public void On_TwoDifferentSignedInPeople_Advance()
    {
        var on = new KafkaOpsOptions().DifferentApprover;
        var request = Pending("ada@example.org");
        Assert.Null(ChangeRequestWorkflow.Approve(request, "bea@example.org", DateTimeOffset.UnixEpoch, on));
        Assert.Null(ChangeRequestWorkflow.MarkExecuting(request, "bea@example.org", DateTimeOffset.UnixEpoch, on));
        Assert.Equal(KafkaChangeStatus.Executing, request.Status);
    }

    [Theory]
    [InlineData("anonymous", "anonymous")]
    [InlineData("anonymous", "bea@example.org")]
    [InlineData("ada@example.org", "anonymous")]
    [InlineData(" Anonymous ", "ANONYMOUS")]
    public void On_AnUnidentifiedPerson_CannotSatisfyEitherApproval(string requester, string approver)
    {
        var on = new KafkaOpsOptions().DifferentApprover;
        var request = Pending(requester);
        Assert.Equal(ChangeRequestWorkflow.UnidentifiedDetail, ChangeRequestWorkflow.Approve(request, approver, DateTimeOffset.UnixEpoch, on));
        Assert.Equal(KafkaChangeStatus.Pending, request.Status);
        Assert.Equal("", request.Approver);
    }

    [Fact]
    public void On_AnApprovalRecordedUnderTheSharedIdentity_CannotBeExecuted()
    {
        var request = Pending("ada@example.org");
        request.Status = KafkaChangeStatus.Approved;
        request.Approver = "anonymous";
        Assert.Equal(ChangeRequestWorkflow.UnidentifiedDetail, ChangeRequestWorkflow.MarkExecuting(request, "cy@example.org", DateTimeOffset.UnixEpoch, true));
        Assert.Equal(ChangeRequestWorkflow.UnidentifiedDetail, ChangeRequestWorkflow.MarkExecuting(Approved("anonymous", "bea@example.org"), "anonymous", DateTimeOffset.UnixEpoch, true));
        Assert.Equal(KafkaChangeStatus.Approved, request.Status);
    }

    [Fact]
    public void On_TheSamePersonUnderTwoSpellings_IsStillOnePerson()
    {
        var request = Pending("Ada@Example.org");
        Assert.Equal("The requester cannot approve their own request.", ChangeRequestWorkflow.Approve(request, " ada@example.org ", DateTimeOffset.UnixEpoch, true));
    }

    [Fact]
    public async Task AuditBudget_StopsWaiting_WhenTheWriteBlocksItsThread()
    {
        using var release = new ManualResetEventSlim(false);
        var watch = Stopwatch.StartNew();
        await Assert.ThrowsAsync<TimeoutException>(() => AuditBudget.WaitAsync(_ =>
        {
            release.Wait(TimeSpan.FromSeconds(30));
            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(200), CancellationToken.None));
        watch.Stop();
        release.Set();
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), "waited " + watch.Elapsed);
    }

    [Fact]
    public async Task AuditBudget_StopsWaiting_WhenTheWriteIgnoresCancellation()
    {
        var never = new TaskCompletionSource();
        var watch = Stopwatch.StartNew();
        await Assert.ThrowsAsync<TimeoutException>(() => AuditBudget.WaitAsync(_ => never.Task, TimeSpan.FromMilliseconds(200), CancellationToken.None));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AuditBudget_ReturnsTheWriteFault_WhenItFailsInTime()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditBudget.WaitAsync(_ => Task.FromException(new InvalidOperationException()), TimeSpan.FromSeconds(3), CancellationToken.None));
        await AuditBudget.WaitAsync(_ => Task.CompletedTask, TimeSpan.FromSeconds(3), CancellationToken.None);
    }

    private static ChangeRequestRecord Pending(string requester) => new()
    {
        Id = Guid.NewGuid(),
        Requester = requester,
        SecondApproverRequired = true,
        Status = KafkaChangeStatus.Pending
    };

    private static ChangeRequestRecord Approved(string requester, string approver)
    {
        var request = Pending(requester);
        request.Status = KafkaChangeStatus.Approved;
        request.Approver = approver;
        return request;
    }
}
