using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition;

[Trait("Category", "UnitTests")]
public class ResourceTypeCountFillServiceTests
{
    private static readonly DateTime CountsTableCreatedAt = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ExecuteAsync_LaterPass_FillsFromAnHourBeforeThePreviousPassStarted()
    {
        var calls = new List<DateTime>();
        var twoPasses = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var manager = new Mock<IDataAcquisitionLogManager>();
        manager
            .Setup(m => m.FillMissingResourceTypeCountsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, CancellationToken>((since, _) => RecordCall(calls, since, twoPasses))
            .ReturnsAsync(3);
        var service = CreateService(manager.Object, CountsTableCreatedAt, TimeSpan.Zero);

        var started = DateTime.UtcNow;
        await service.StartAsync(CancellationToken.None);
        await twoPasses.Task;
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(CountsTableCreatedAt.AddDays(-1), calls[0]);
        Assert.InRange(calls[1], started.AddHours(-1).AddSeconds(-5), DateTime.UtcNow.AddHours(-1));
        Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ExecuteAsync_FirstPassFails_RetriesTheSameWindowOnTheNextPass()
    {
        var calls = new List<DateTime>();
        var twoPasses = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        var manager = new Mock<IDataAcquisitionLogManager>();
        manager
            .Setup(m => m.FillMissingResourceTypeCountsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns((DateTime since, CancellationToken _) =>
            {
                RecordCall(calls, since, twoPasses);
                return Interlocked.Increment(ref attempts) == 1
                    ? Task.FromException<int>(new InvalidOperationException("database unavailable"))
                    : Task.FromResult(0);
            });
        var service = CreateService(manager.Object, CountsTableCreatedAt, TimeSpan.Zero);

        await service.StartAsync(CancellationToken.None);
        await twoPasses.Task;
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(CountsTableCreatedAt.AddDays(-1), calls[0]);
        Assert.Equal(CountsTableCreatedAt.AddDays(-1), calls[1]);
        Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ExecuteAsync_CountsTableMissing_FillsNothingAndKeepsChecking()
    {
        var twoChecks = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var checks = 0;
        var manager = new Mock<IDataAcquisitionLogManager>();
        var queries = new Mock<IDataAcquisitionLogQueries>();
        queries
            .Setup(q => q.GetResourceTypeCountsCreatedAtAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                if (Interlocked.Increment(ref checks) == 2)
                {
                    twoChecks.TrySetResult();
                }
            })
            .ReturnsAsync((DateTime?)null);
        var service = CreateService(manager.Object, queries.Object, TimeSpan.Zero);

        await service.StartAsync(CancellationToken.None);
        await twoChecks.Task;
        await service.StopAsync(CancellationToken.None);

        Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
        manager.Verify(m => m.FillMissingResourceTypeCountsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
                       Times.Never);
    }

    [Fact]
    public async Task StopAsync_WhileFilling_StopsWithoutThrowing()
    {
        var fillStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var manager = new Mock<IDataAcquisitionLogManager>();
        manager
            .Setup(m => m.FillMissingResourceTypeCountsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(async (DateTime _, CancellationToken token) =>
            {
                fillStarted.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                return 0;
            });
        var service = CreateService(manager.Object, CountsTableCreatedAt, TimeSpan.Zero);

        await service.StartAsync(CancellationToken.None);
        // ExecuteAsync starts on a background task; stopping before it runs would cancel it unrun.
        await fillStarted.Task;
        await service.StopAsync(CancellationToken.None);

        Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task StopAsync_BetweenPasses_StopsWithoutThrowing()
    {
        var firstPassDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var manager = new Mock<IDataAcquisitionLogManager>();
        manager
            .Setup(m => m.FillMissingResourceTypeCountsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback(() => firstPassDone.TrySetResult())
            .ReturnsAsync(0);
        var service = CreateService(manager.Object, CountsTableCreatedAt, TimeSpan.FromHours(1));

        await service.StartAsync(CancellationToken.None);
        await firstPassDone.Task;
        await service.StopAsync(CancellationToken.None);

        Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
        manager.Verify(m => m.FillMissingResourceTypeCountsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
                       Times.Once);
    }

    private static void RecordCall(List<DateTime> calls, DateTime since, TaskCompletionSource twoPasses)
    {
        lock (calls)
        {
            calls.Add(since);
            if (calls.Count == 2)
            {
                twoPasses.TrySetResult();
            }
        }
    }

    private static ResourceTypeCountFillService CreateService(IDataAcquisitionLogManager manager,
                                                              DateTime? createdAt,
                                                              TimeSpan passInterval)
    {
        var queries = new Mock<IDataAcquisitionLogQueries>();
        queries
            .Setup(q => q.GetResourceTypeCountsCreatedAtAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdAt);

        return CreateService(manager, queries.Object, passInterval);
    }

    private static ResourceTypeCountFillService CreateService(IDataAcquisitionLogManager manager,
                                                              IDataAcquisitionLogQueries queries,
                                                              TimeSpan passInterval)
    {
        var provider = new ServiceCollection()
            .AddScoped(_ => manager)
            .AddScoped(_ => queries)
            .BuildServiceProvider();

        return new ResourceTypeCountFillService(provider.GetRequiredService<IServiceScopeFactory>(),
                                                NullLogger<ResourceTypeCountFillService>.Instance,
                                                passInterval);
    }
}
