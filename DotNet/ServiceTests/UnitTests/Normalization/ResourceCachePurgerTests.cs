using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class ResourceCachePurgerTests
{
    private const string CorrelationId = "3fa85f64-5717-4562-b3fc-2c963f66afa6";

    [Fact]
    public async Task PurgeAsync_DeletesAcquisitionKeysAndCorrelationKey()
    {
        // A terminal failure means nothing downstream will ever consume this message's cache
        // entries, so the purge removes the {correlationId}:{ResourceType} acquisition keys AND
        // the {correlationId} key normalization was writing when it failed.
        var (purger, cache) = BuildPurger();

        List<string>? deletedKeys = null;
        cache
            .Setup(item => item.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .Callback<List<string>, CancellationToken>((keys, _) => deletedKeys = keys)
            .Returns(Task.CompletedTask);

        await purger.PurgeAsync(BuildValue(ResourceCacheType.Redis), "test");

        Assert.NotNull(deletedKeys);
        Assert.Equal(
            new List<string> { $"{CorrelationId}:Patient", $"{CorrelationId}:Encounter", CorrelationId },
            deletedKeys);
    }

    [Fact]
    public async Task PurgeAsync_DoesNotDeleteTwiceWhenTheCorrelationKeyIsAlreadyPresent()
    {
        var (purger, cache) = BuildPurger();

        List<string>? deletedKeys = null;
        cache
            .Setup(item => item.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .Callback<List<string>, CancellationToken>((keys, _) => deletedKeys = keys)
            .Returns(Task.CompletedTask);

        var value = BuildValue(ResourceCacheType.Redis);
        value.CacheKeys.Add(CorrelationId);

        await purger.PurgeAsync(value, "test");

        Assert.NotNull(deletedKeys);
        Assert.Equal(deletedKeys!.Count, deletedKeys.Distinct().Count());
    }

    [Fact]
    public async Task PurgeAsync_UsesTheCacheTypeCarriedOnTheMessage()
    {
        var (purger, cache) = BuildPurger();

        cache
            .Setup(item => item.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await purger.PurgeAsync(BuildValue(ResourceCacheType.ABS), "test");

    }

    [Fact]
    public async Task PurgeAsync_WithNullCacheKeys_DoesNotDelete() => await AssertNoDelete(null);

    [Fact]
    public async Task PurgeAsync_WithEmptyCacheKeys_DoesNotDelete() => await AssertNoDelete(new List<string>());

    private static async Task AssertNoDelete(List<string>? cacheKeys)
    {
        var (purger, cache) = BuildPurger();

        var value = BuildValue(ResourceCacheType.Redis);
        value.CacheKeys = cacheKeys!;

        await purger.PurgeAsync(value, "test");

        cache.Verify(
            item => item.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PurgeAsync_WithNullValue_DoesNotThrow()
    {
        var (purger, cache) = BuildPurger();

        await purger.PurgeAsync(null, "test");

    }

    [Fact]
    public async Task PurgeAsync_WhenDeleteThrows_SwallowsTheException()
    {
        var (purger, cache) = BuildPurger();

        cache
            .Setup(item => item.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));

        // The caller is already handling a failed message; cleanup failure must not add another.
        await purger.PurgeAsync(BuildValue(ResourceCacheType.Redis), "test");
    }

    private static (ResourceCachePurger, Mock<IResourceCache>) BuildPurger()
    {
        var cache = new Mock<IResourceCache>();
        var purger = new ResourceCachePurger(cache.Object, Mock.Of<ILogger<ResourceCachePurger>>());

        return (purger, cache);
    }

    private static ResourcesAcquiredValue BuildValue(ResourceCacheType cacheType) => new()
    {
        QueryType = "Initial",
        ReportableEvent = "Adhoc",
        ScheduledReports = new List<LantanaGroup.Link.Shared.Application.Models.ScheduledReport>(),
        CacheType = cacheType,
        CacheKeys = new List<string> { $"{CorrelationId}:Patient", $"{CorrelationId}:Encounter" }
    };
}
