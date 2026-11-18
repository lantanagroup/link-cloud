using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition;

[Trait("Category", "UnitTests")]
public class ResourceTypeCountFillServiceTests
{
    [Fact]
    public async Task StartAsync_OnStartup_FillsCountsForLogsModifiedInTheLastDay()
    {
        DateTime? modifiedSince = null;
        var manager = new Mock<IDataAcquisitionLogManager>();
        manager
            .Setup(m => m.FillMissingResourceTypeCountsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, CancellationToken>((since, _) => modifiedSince = since)
            .ReturnsAsync(3);
        var service = CreateService(manager.Object);

        var before = DateTime.UtcNow;
        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!;

        Assert.NotNull(modifiedSince);
        Assert.InRange(modifiedSince.Value, before.AddDays(-1).AddSeconds(-5), DateTime.UtcNow.AddDays(-1));
    }

    [Fact]
    public async Task StartAsync_FillFails_DoesNotThrow()
    {
        var manager = new Mock<IDataAcquisitionLogManager>();
        manager
            .Setup(m => m.FillMissingResourceTypeCountsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        var service = CreateService(manager.Object);

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!;

        Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
    }

    private static ResourceTypeCountFillService CreateService(IDataAcquisitionLogManager manager)
    {
        var provider = new ServiceCollection()
            .AddScoped(_ => manager)
            .BuildServiceProvider();

        return new ResourceTypeCountFillService(provider.GetRequiredService<IServiceScopeFactory>(),
                                                NullLogger<ResourceTypeCountFillService>.Instance);
    }
}
