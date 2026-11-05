using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using UnitTests.Shared.Auditing;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Report;

/// <summary>
/// Covers the startup hook that logs the retention participants, reconciles the retention job and leaves the
/// scheduler's lifecycle to RetryScheduleService.
/// </summary>
[Trait("Category", "UnitTests")]
public class RetentionScheduleHostedServiceTests
{
    private readonly Mock<IRetentionScheduleReconciler> _reconciler = new();
    private readonly CapturingLogger<RetentionScheduleHostedService> _logger = new();

    [Fact]
    public async Task StartAsync_Called_ReconcilesWithStartupToken()
    {
        using var cancellation = new CancellationTokenSource();

        await CreateService().StartAsync(cancellation.Token);

        _reconciler.Verify(r => r.ReconcileAsync(cancellation.Token), Times.Once);
        _reconciler.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StartAsync_ReconcilerThrows_LogsErrorAndContinues()
    {
        _reconciler
            .Setup(r => r.ReconcileAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("store unreachable"));

        var exception = await Record.ExceptionAsync(() => CreateService().StartAsync(CancellationToken.None));

        Assert.Null(exception);
        Assert.Single(_logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task StartAsync_StartupCanceled_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _reconciler
            .Setup(r => r.ReconcileAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService().StartAsync(cancellation.Token));

        Assert.DoesNotContain(_logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task StartAsync_CanceledByAnotherToken_LogsError()
    {
        // A timeout that surfaces as a cancellation is a failure, not startup being canceled.
        _reconciler
            .Setup(r => r.ReconcileAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("timed out"));

        var exception = await Record.ExceptionAsync(() => CreateService().StartAsync(CancellationToken.None));

        Assert.Null(exception);
        Assert.Single(_logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task StopAsync_Called_DoesNotTouchReconciler()
    {
        await CreateService().StopAsync(CancellationToken.None);

        _reconciler.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StartAsync_DefaultParticipants_LogsThemAsInformation()
    {
        await CreateService().StartAsync(CancellationToken.None);

        var entry = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Information);
        Assert.Contains("DataAcquisition (Report), the default", entry.Message);
    }

    [Fact]
    public async Task StartAsync_InvalidParticipants_LogsAnErrorAndStillReconciles()
    {
        var participants = ReadParticipants(new Dictionary<string, string?>
        {
            ["Retention:Participants:0:ServiceName"] = "Report"
        });

        await CreateService(participants).StartAsync(CancellationToken.None);

        var entry = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("has no Scope", entry.Message);
        _reconciler.Verify(r => r.ReconcileAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_ReconcilerThrowsWithInvalidParticipants_LogsBothErrorsParticipantsFirst()
    {
        _reconciler
            .Setup(r => r.ReconcileAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("store unreachable"));
        var participants = ReadParticipants(new Dictionary<string, string?>
        {
            ["Retention:Participants"] = ""
        });

        await CreateService(participants).StartAsync(CancellationToken.None);

        var errors = _logger.Entries.Where(e => e.Level == LogLevel.Error).ToList();
        Assert.Equal(2, errors.Count);
        Assert.Contains("lists no participants", errors[0].Message);
        Assert.Contains("Failed to reconcile", errors[1].Message);
    }

    private RetentionScheduleHostedService CreateService(RetentionParticipants? participants = null) =>
        new(_reconciler.Object, participants ?? ReadParticipants(new Dictionary<string, string?>()), _logger);

    private static RetentionParticipants ReadParticipants(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return RetentionParticipants.FromConfiguration(configuration);
    }
}
