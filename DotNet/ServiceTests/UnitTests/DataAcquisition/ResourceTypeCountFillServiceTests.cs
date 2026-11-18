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
    public async Task StartAsync_CountsTableExists_FillsTwiceFromADayBeforeItWasCreated()
    {
        var modifiedSince = new List<DateTime>();
        var manager = new Mock<IDataAcquisitionLogManager>();
        manager
            .Setup(m => m.FillMissingResourceTypeCountsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, CancellationToken>((since, _) => modifiedSince.Add(since))
            .ReturnsAsync(3);
        var service = CreateService(manager.Object, CountsTableCreatedAt, TimeSpan.Zero);

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!;

        var expected = CountsTableCreatedAt.AddDays(-1);
        Assert.Equal([expected, expected], modifiedSince);
    }

    [Fact]
    public async Task StartAsync_CountsTableMissing_FillsNothing()
    {
        var manager = new Mock<IDataAcquisitionLogManager>();
        var service = CreateService(manager.Object, createdAt: null, TimeSpan.Zero);

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!;

        Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
        manager.Verify(m => m.FillMissingResourceTypeCountsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
                       Times.Never);
    }

    [Fact]
    public async Task StartAsync_FillFails_DoesNotThrow()
    {
        var manager = new Mock<IDataAcquisitionLogManager>();
        manager
            .Setup(m => m.FillMissingResourceTypeCountsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        var service = CreateService(manager.Object, CountsTableCreatedAt, TimeSpan.Zero);

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!;

        Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
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

    private static ResourceTypeCountFillService CreateService(IDataAcquisitionLogManager manager,
                                                              DateTime? createdAt,
                                                              TimeSpan secondPassDelay)
    {
        var queries = new Mock<IDataAcquisitionLogQueries>();
        queries
            .Setup(q => q.GetResourceTypeCountsCreatedAtAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdAt);

        var provider = new ServiceCollection()
            .AddScoped(_ => manager)
            .AddScoped(_ => queries.Object)
            .BuildServiceProvider();

        return new ResourceTypeCountFillService(provider.GetRequiredService<IServiceScopeFactory>(),
                                                NullLogger<ResourceTypeCountFillService>.Instance,
                                                secondPassDelay);
    }
}
