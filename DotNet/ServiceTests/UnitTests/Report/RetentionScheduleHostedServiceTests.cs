using LantanaGroup.Link.Report.Jobs;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Report;

/// <summary>
/// Covers the startup hook that reconciles the retention job and leaves the scheduler's lifecycle to
/// RetryScheduleService.
/// </summary>
[Trait("Category", "UnitTests")]
public class RetentionScheduleHostedServiceTests
{
    private readonly Mock<IRetentionScheduleReconciler> _reconciler = new();

    [Fact]
    public async Task StartAsync_Called_ReconcilesWithStartupToken()
    {
        using var cancellation = new CancellationTokenSource();

        await new RetentionScheduleHostedService(_reconciler.Object).StartAsync(cancellation.Token);

        _reconciler.Verify(r => r.ReconcileAsync(cancellation.Token), Times.Once);
        _reconciler.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StartAsync_ReconcilerThrows_Propagates()
    {
        var failure = new InvalidOperationException("store unreachable");
        _reconciler
            .Setup(r => r.ReconcileAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new RetentionScheduleHostedService(_reconciler.Object).StartAsync(CancellationToken.None));

        Assert.Same(failure, thrown);
    }

    [Fact]
    public async Task StopAsync_Called_DoesNotTouchReconciler()
    {
        await new RetentionScheduleHostedService(_reconciler.Object).StopAsync(CancellationToken.None);

        _reconciler.VerifyNoOtherCalls();
    }
}
