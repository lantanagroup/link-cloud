using System.Collections.Concurrent;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Domain.Models;
using LantanaGroup.Link.Report.Jobs;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.AdoJobStore;
using Quartz.Spi;
using ServiceTests.TestHelpers;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report;

/// <summary>
/// Runs the retention job on two clustered Quartz schedulers sharing Report's migrated SQL Server store, the
/// way two Report instances do, and checks that each firing runs once, firings never overlap, and a run cut
/// off by a dead instance is recovered by the other.
/// </summary>
/// <remarks>
/// Each test uses its own scheduler name, which Quartz uses as the cluster's key in every QRTZ_ table, so tests
/// share the fixture's database without seeing each other's rows.
/// </remarks>
[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class RetentionScheduleClusterTests
{
    private const string EverySecond = "* * * * * ?";

    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(45);

    private readonly string _connectionString;

    public RetentionScheduleClusterTests(ReportSqlServerIntegrationTestFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task TwoInstances_EachFiring_RunsOnce()
    {
        var cluster = NewClusterName();
        var recorder = new RetentionRecorder { Hold = TimeSpan.FromMilliseconds(200) };

        try
        {
            await using var nodeA = await StartNodeAsync(cluster, "node-a", recorder);
            await using var nodeB = await StartNodeAsync(cluster, "node-b", recorder);

            Assert.NotSame(nodeA.Scheduler, nodeB.Scheduler);
            Assert.Equal("node-b", nodeB.Scheduler.SchedulerInstanceId);

            await WaitUntilAsync(() => recorder.Calls.Count >= 5, "five retention runs");

            var repeated = recorder.Calls
                .GroupBy(c => c.Run.ScheduledFireTimeUtc)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.Empty(repeated);
            Assert.All(recorder.Calls, c => Assert.False(c.Run.IsRecovery));
        }
        finally
        {
            await DeleteClusterRowsAsync(cluster);
        }
    }

    [Fact]
    public async Task TwoInstances_RunOutlastsInterval_RunsNeverOverlap()
    {
        var cluster = NewClusterName();
        var recorder = new RetentionRecorder { Hold = TimeSpan.FromMilliseconds(2500) };

        try
        {
            await using var nodeA = await StartNodeAsync(cluster, "node-a", recorder);
            await using var nodeB = await StartNodeAsync(cluster, "node-b", recorder);

            await WaitUntilAsync(() => recorder.Completed >= 3, "three retention runs to finish");

            Assert.Equal(1, recorder.MaxConcurrent);
        }
        finally
        {
            await DeleteClusterRowsAsync(cluster);
        }
    }

    [Fact]
    public async Task InstanceDiesMidRun_OtherInstance_RecoversWithOriginalFireTime()
    {
        var cluster = NewClusterName();
        var recorder = new RetentionRecorder
        {
            Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        };

        try
        {
            var nodeA = await StartNodeAsync(cluster, "node-a", recorder);
            await WaitUntilAsync(() => recorder.Calls.Any(c => c.Node == "node-a"), "node-a to start a run");
            var interrupted = recorder.Calls.First(c => c.Node == "node-a").Run;

            await using var nodeB = await StartNodeAsync(cluster, "node-b", recorder);

            // Stop node-a without letting its run finish, then age its last check-in so node-b treats it as
            // dead now rather than deferring recovery of a job that might still be running there.
            await nodeA.Scheduler.Shutdown(waitForJobsToComplete: false);
            await BackdateCheckinAsync(cluster, "node-a", TimeSpan.FromHours(1));

            await WaitUntilAsync(() => recorder.Calls.Any(c => c.Node == "node-b" && c.Run.IsRecovery),
                                 "node-b to run a recovery");

            var recovery = recorder.Calls.First(c => c.Node == "node-b" && c.Run.IsRecovery).Run;

            // Quartz records the original fire time to the second.
            Assert.Equal(TruncateToSecond(interrupted.ScheduledFireTimeUtc),
                         TruncateToSecond(recovery.ScheduledFireTimeUtc));
            Assert.NotEqual(interrupted.FireInstanceId, recovery.FireInstanceId);

            await nodeA.DisposeAsync();
        }
        finally
        {
            recorder.Gate.TrySetResult();
            await DeleteClusterRowsAsync(cluster);
        }
    }

    private static string NewClusterName() => $"retention-cluster-{Guid.NewGuid():N}";

    private static DateTimeOffset TruncateToSecond(DateTimeOffset value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, value.Offset);

    private async Task<ClusterNode> StartNodeAsync(string cluster, string nodeId, RetentionRecorder recorder)
    {
        var services = new ServiceCollection();

        // First, so AddLogging's TryAdd keeps the shared factory; see AddQuartzTestLogging.
        services.AddQuartzTestLogging();
        services.AddLogging();
        services.AddSingleton<IRetentionCheckWorker>(new RecordingWorker(recorder, nodeId));
        services.AddSingleton(Options.Create(new RetentionScheduleSettings { Cron = EverySecond }));
        services.AddSingleton<IRetentionScheduleReconciler, RetentionScheduleReconciler>();

        // The store settings RegisterQuartzDatabase uses, with a fixed instance id per node and fast
        // check-ins so a dead node is noticed in seconds.
        services.AddQuartz(q =>
        {
            q.SchedulerName = cluster;
            q.SchedulerId = nodeId;
            q.UsePersistentStore(store =>
            {
                store.UseSqlServer(sql =>
                {
                    sql.UseDriverDelegate<SqlServerDelegate>();
                    sql.ConnectionString = _connectionString;
                    sql.TablePrefix = "quartz.QRTZ_";
                });
                store.UseSystemTextJsonSerializer();
                store.UseClustering(clustering =>
                {
                    clustering.CheckinInterval = TimeSpan.FromSeconds(1);
                    clustering.CheckinMisfireThreshold = TimeSpan.FromSeconds(1);
                });
            });
        });

        var provider = services.BuildServiceProvider();
        var scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler();
        scheduler.JobFactory = provider.GetRequiredService<IJobFactory>();

        await provider.GetRequiredService<IRetentionScheduleReconciler>().ReconcileAsync(CancellationToken.None);

        // Quartz keeps one scheduler per name per process. Unregister this node's so the next node with the
        // same cluster name gets a scheduler of its own instead of this one.
        SchedulerRepository.Instance.Remove(cluster);

        await scheduler.Start();

        return new ClusterNode(provider, scheduler);
    }

    private async Task BackdateCheckinAsync(string cluster, string nodeId, TimeSpan by)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "UPDATE quartz.QRTZ_SCHEDULER_STATE SET LAST_CHECKIN_TIME = LAST_CHECKIN_TIME - @by " +
            "WHERE SCHED_NAME = @cluster AND INSTANCE_NAME = @node";
        command.Parameters.AddWithValue("@by", by.Ticks);
        command.Parameters.AddWithValue("@cluster", cluster);
        command.Parameters.AddWithValue("@node", nodeId);

        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private async Task DeleteClusterRowsAsync(string cluster)
    {
        string[] tables =
        [
            "QRTZ_FIRED_TRIGGERS", "QRTZ_PAUSED_TRIGGER_GRPS", "QRTZ_SCHEDULER_STATE", "QRTZ_LOCKS",
            "QRTZ_SIMPLE_TRIGGERS", "QRTZ_SIMPROP_TRIGGERS", "QRTZ_CRON_TRIGGERS", "QRTZ_BLOB_TRIGGERS",
            "QRTZ_TRIGGERS", "QRTZ_JOB_DETAILS", "QRTZ_CALENDARS"
        ];

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var table in tables)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM quartz.{table} WHERE SCHED_NAME = @cluster";
            command.Parameters.AddWithValue("@cluster", cluster);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + WaitTimeout;

        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"Timed out after {WaitTimeout.TotalSeconds} s waiting for {what}.");
            }

            await Task.Delay(100);
        }
    }

    private sealed class ClusterNode : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;

        public ClusterNode(ServiceProvider provider, IScheduler scheduler)
        {
            _provider = provider;
            Scheduler = scheduler;
        }

        public IScheduler Scheduler { get; }

        public async ValueTask DisposeAsync()
        {
            if (!Scheduler.IsShutdown)
            {
                await Scheduler.Shutdown(waitForJobsToComplete: false);
            }

            await _provider.DisposeAsync();
        }
    }

    private sealed class RetentionRecorder
    {
        private int _running;
        private int _maxConcurrent;
        private int _completed;

        public ConcurrentQueue<(string Node, RetentionCheckRun Run)> Calls { get; } = new();

        public TimeSpan Hold { get; init; }

        public TaskCompletionSource? Gate { get; init; }

        public int MaxConcurrent => Volatile.Read(ref _maxConcurrent);

        public int Completed => Volatile.Read(ref _completed);

        public async Task RecordAsync(string node, RetentionCheckRun run)
        {
            Calls.Enqueue((node, run));

            var running = Interlocked.Increment(ref _running);
            var max = Volatile.Read(ref _maxConcurrent);
            while (running > max && Interlocked.CompareExchange(ref _maxConcurrent, running, max) != max)
            {
                max = Volatile.Read(ref _maxConcurrent);
            }

            try
            {
                if (Gate is not null)
                {
                    await Gate.Task;
                }
                else
                {
                    await Task.Delay(Hold);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _running);
                Interlocked.Increment(ref _completed);
            }
        }
    }

    private sealed class RecordingWorker : IRetentionCheckWorker
    {
        private readonly RetentionRecorder _recorder;
        private readonly string _node;

        public RecordingWorker(RetentionRecorder recorder, string node)
        {
            _recorder = recorder;
            _node = node;
        }

        public Task RunAsync(RetentionCheckRun run, CancellationToken cancellationToken) =>
            _recorder.RecordAsync(_node, run);
    }
}
