using System.Globalization;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Domain.Models;
using LantanaGroup.Link.Report.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using UnitTests.Shared.Auditing;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Report;

/// <summary>
/// Covers what the retention job hands to <see cref="IRetentionCheckWorker"/> on each firing: the fire time, whether the
/// run is a cluster recovery, the fire instance id and the cancellation token, and what it does when the check fails.
/// </summary>
[Trait("Category", "UnitTests")]
public class RetentionCheckJobTests
{
    private static readonly DateTimeOffset ScheduledFireTime = new(2026, 11, 2, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RecoveryFireTime = new(2026, 11, 2, 3, 10, 0, TimeSpan.Zero);

    private readonly RetentionCheckSpy _spy = new();
    private readonly CapturingLogger<RetentionCheckJob> _logger = new();
    private readonly ServiceProvider _provider;

    public RetentionCheckJobTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_spy);
        services.AddScoped<IRetentionCheckWorker, SpyRetentionCheck>();
        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public void RetentionCheckJob_Type_DisallowsConcurrentExecution()
    {
        Assert.True(Attribute.IsDefined(typeof(RetentionCheckJob), typeof(DisallowConcurrentExecutionAttribute)));
    }

    [Fact]
    public async Task Execute_Fires_CallsRetentionCheckOnceWithFireTime()
    {
        var context = CreateContext(scheduledFireTime: ScheduledFireTime, fireInstanceId: "fire-1");

        await CreateJob().Execute(context.Object);

        var call = Assert.Single(_spy.Calls);
        Assert.Equal(ScheduledFireTime, call.Run.ScheduledFireTimeUtc);
        Assert.False(call.Run.IsRecovery);
        Assert.Equal("fire-1", call.Run.FireInstanceId);
    }

    [Fact]
    public async Task Execute_NoScheduledFireTime_FallsBackToFireTime()
    {
        var context = CreateContext(scheduledFireTime: null, fireTime: RecoveryFireTime);

        await CreateJob().Execute(context.Object);

        Assert.Equal(RecoveryFireTime, Assert.Single(_spy.Calls).Run.ScheduledFireTimeUtc);
    }

    [Fact]
    public async Task Execute_Called_PassesJobCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        var context = CreateContext(scheduledFireTime: ScheduledFireTime, cancellationToken: cancellation.Token);

        await CreateJob().Execute(context.Object);

        Assert.Equal(cancellation.Token, Assert.Single(_spy.Calls).CancellationToken);
    }

    [Fact]
    public async Task Execute_TwoFirings_ResolveCheckInSeparateScopesAndDisposeEach()
    {
        var job = CreateJob();

        await job.Execute(CreateContext(scheduledFireTime: ScheduledFireTime).Object);
        await job.Execute(CreateContext(scheduledFireTime: ScheduledFireTime.AddDays(1)).Object);

        Assert.Equal(2, _spy.Calls.Count);
        Assert.NotEqual(_spy.Calls[0].InstanceId, _spy.Calls[1].InstanceId);
        Assert.Equal(2, _spy.DisposedCount);
    }

    [Fact]
    public async Task Execute_Recovering_PassesRecoveryAndOriginalFireTime()
    {
        var context = CreateContext(scheduledFireTime: RecoveryFireTime,
                                    recovering: true,
                                    originalFireTime: QuartzRecoveryValue(ScheduledFireTime));

        await CreateJob().Execute(context.Object);

        var run = Assert.Single(_spy.Calls).Run;
        Assert.True(run.IsRecovery);
        Assert.Equal(ScheduledFireTime, run.ScheduledFireTimeUtc);
    }

    [Fact]
    public async Task Execute_RecoveringWithoutOriginalFireTime_UsesScheduledFireTime()
    {
        var context = CreateContext(scheduledFireTime: RecoveryFireTime, recovering: true);

        await CreateJob().Execute(context.Object);

        var run = Assert.Single(_spy.Calls).Run;
        Assert.True(run.IsRecovery);
        Assert.Equal(RecoveryFireTime, run.ScheduledFireTimeUtc);
    }

    [Fact]
    public async Task Execute_RecoveringWithUnparseableOriginalFireTime_UsesScheduledFireTime()
    {
        var context = CreateContext(scheduledFireTime: RecoveryFireTime,
                                    recovering: true,
                                    originalFireTime: "not a date");

        await CreateJob().Execute(context.Object);

        Assert.Equal(RecoveryFireTime, Assert.Single(_spy.Calls).Run.ScheduledFireTimeUtc);
    }

    [Fact]
    public async Task Execute_NotRecovering_IgnoresOriginalFireTimeInDataMap()
    {
        var context = CreateContext(scheduledFireTime: RecoveryFireTime,
                                    recovering: false,
                                    originalFireTime: QuartzRecoveryValue(ScheduledFireTime));

        await CreateJob().Execute(context.Object);

        Assert.Equal(RecoveryFireTime, Assert.Single(_spy.Calls).Run.ScheduledFireTimeUtc);
    }

    [Fact]
    public async Task Execute_Called_LogsStartAndFinish()
    {
        await CreateJob().Execute(CreateContext(scheduledFireTime: ScheduledFireTime).Object);

        Assert.Equal(2, _logger.Entries.Count(e => e.Level == LogLevel.Information));
    }

    [Fact]
    public async Task Execute_CheckThrows_ThrowsJobExecutionExceptionWithoutRefire()
    {
        var failure = new InvalidOperationException("selection failed");
        _spy.OnRun = () => throw failure;

        var thrown = await Assert.ThrowsAsync<JobExecutionException>(
            () => CreateJob().Execute(CreateContext(scheduledFireTime: ScheduledFireTime).Object));

        Assert.False(thrown.RefireImmediately);
        Assert.Same(failure, thrown.InnerException);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Execute_Canceled_PropagatesOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _spy.OnRun = () => throw new OperationCanceledException(cancellation.Token);
        var context = CreateContext(scheduledFireTime: ScheduledFireTime, cancellationToken: cancellation.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateJob().Execute(context.Object));
    }

    private RetentionCheckJob CreateJob() =>
        new(_provider.GetRequiredService<IServiceScopeFactory>(), _logger);

    private static Mock<IJobExecutionContext> CreateContext(DateTimeOffset? scheduledFireTime,
                                                            DateTimeOffset? fireTime = null,
                                                            bool recovering = false,
                                                            string? originalFireTime = null,
                                                            string fireInstanceId = "fire-instance",
                                                            CancellationToken cancellationToken = default)
    {
        var dataMap = new JobDataMap();
        if (originalFireTime is not null)
        {
            dataMap[SchedulerConstants.FailedJobOriginalTriggerFiretime] = originalFireTime;
        }

        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(c => c.ScheduledFireTimeUtc).Returns(scheduledFireTime);
        context.SetupGet(c => c.FireTimeUtc).Returns(fireTime ?? scheduledFireTime ?? RecoveryFireTime);
        context.SetupGet(c => c.Recovering).Returns(recovering);
        context.SetupGet(c => c.MergedJobDataMap).Returns(dataMap);
        context.SetupGet(c => c.FireInstanceId).Returns(fireInstanceId);
        context.SetupGet(c => c.CancellationToken).Returns(cancellationToken);

        return context;
    }

    // Quartz 3.18.1's JobStoreSupport.ClusterRecover writes only the original *actual* fire time, as
    // Convert.ToString((object)DateTimeOffset, CultureInfo.InvariantCulture). It never writes the
    // FailedJobOriginalTriggerScheduledFiretime key, despite the constant existing.
    private static string QuartzRecoveryValue(DateTimeOffset fireTime) =>
        Convert.ToString((object)fireTime, CultureInfo.InvariantCulture)!;

    private sealed class RetentionCheckSpy
    {
        public List<(RetentionCheckRun Run, CancellationToken CancellationToken, Guid InstanceId)> Calls { get; } = [];

        public int DisposedCount { get; set; }

        public Action? OnRun { get; set; }
    }

    private sealed class SpyRetentionCheck : IRetentionCheckWorker, IDisposable
    {
        private readonly RetentionCheckSpy _spy;
        private readonly Guid _instanceId = Guid.NewGuid();

        public SpyRetentionCheck(RetentionCheckSpy spy)
        {
            _spy = spy;
        }

        public Task RunAsync(RetentionCheckRun run, CancellationToken cancellationToken)
        {
            _spy.Calls.Add((run, cancellationToken, _instanceId));
            _spy.OnRun?.Invoke();

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _spy.DisposedCount++;
        }
    }
}
