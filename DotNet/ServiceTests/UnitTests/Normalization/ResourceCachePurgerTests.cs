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
    public async Task PurgeAsync_ScopeAll_DeletesAcquisitionKeysAndCorrelationKey()
    {
        var (purger, cache, implementation) = BuildPurger(ResourceCacheType.Redis);

        List<string>? deletedKeys = null;
        implementation
            .Setup(item => item.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .Callback<List<string>, CancellationToken>((keys, _) => deletedKeys = keys)
            .Returns(Task.CompletedTask);

        await purger.PurgeAsync(BuildValue(ResourceCacheType.Redis), "test", ResourceCachePurgeScope.All);

        cache.Verify(item => item.GetImplementation(ResourceCacheType.Redis), Times.Once);
        Assert.NotNull(deletedKeys);
        Assert.Equal(
            new List<string> { $"{CorrelationId}:Patient", $"{CorrelationId}:Encounter", CorrelationId },
            deletedKeys);
    }

    [Fact]
    public async Task PurgeAsync_ScopeAcquisitionKeysOnly_LeavesTheCorrelationKey()
    {
        // The retry-exhausted path cannot prove that an earlier attempt did not already publish
        // ResourcesNormalized (the produce precedes the acquisition-key delete on the success path),
        // so Measure Eval may be holding {correlationId} for its SUPPLEMENTAL pass. That key must
        // survive this purge; the cache expiration policy reclaims it if nothing needed it.
        var (purger, _, implementation) = BuildPurger(ResourceCacheType.Redis);

        List<string>? deletedKeys = null;
        implementation
            .Setup(item => item.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .Callback<List<string>, CancellationToken>((keys, _) => deletedKeys = keys)
            .Returns(Task.CompletedTask);

        await purger.PurgeAsync(BuildValue(ResourceCacheType.Redis), "test", ResourceCachePurgeScope.AcquisitionKeysOnly);

        Assert.NotNull(deletedKeys);
        Assert.Equal(
            new List<string> { $"{CorrelationId}:Patient", $"{CorrelationId}:Encounter" },
            deletedKeys);
        Assert.DoesNotContain(CorrelationId, deletedKeys!);
    }

    [Fact]
    public async Task PurgeAsync_UsesTheCacheTypeCarriedOnTheMessage()
    {
        var (purger, cache, implementation) = BuildPurger(ResourceCacheType.ABS);

        implementation
            .Setup(item => item.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await purger.PurgeAsync(BuildValue(ResourceCacheType.ABS), "test", ResourceCachePurgeScope.All);

        cache.Verify(item => item.GetImplementation(ResourceCacheType.ABS), Times.Once);
    }

    [Fact]
    public async Task PurgeAsync_DoesNotDeleteTwiceWhenTheCorrelationKeyIsAlreadyPresent()
    {
        var (purger, _, implementation) = BuildPurger(ResourceCacheType.Redis);

        List<string>? deletedKeys = null;
        implementation
            .Setup(item => item.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .Callback<List<string>, CancellationToken>((keys, _) => deletedKeys = keys)
            .Returns(Task.CompletedTask);

        var value = BuildValue(ResourceCacheType.Redis);
        value.CacheKeys.Add(CorrelationId);

        await purger.PurgeAsync(value, "test", ResourceCachePurgeScope.All);

        Assert.NotNull(deletedKeys);
        Assert.Equal(deletedKeys!.Count, deletedKeys.Distinct().Count());
    }

    [Fact]
    public async Task PurgeAsync_WithNullCacheKeys_DoesNotDelete() => await AssertNoDelete(null);

    [Fact]
    public async Task PurgeAsync_WithEmptyCacheKeys_DoesNotDelete() => await AssertNoDelete(new List<string>());

    private static async Task AssertNoDelete(List<string>? cacheKeys)
    {
        var (purger, cache, implementation) = BuildPurger(ResourceCacheType.Redis);

        var value = BuildValue(ResourceCacheType.Redis);
        value.CacheKeys = cacheKeys!;

        await purger.PurgeAsync(value, "test", ResourceCachePurgeScope.All);

        cache.Verify(item => item.GetImplementation(It.IsAny<ResourceCacheType>()), Times.Never);
        implementation.Verify(
            item => item.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PurgeAsync_WithNullValue_DoesNotThrow()
    {
        var (purger, cache, _) = BuildPurger(ResourceCacheType.Redis);

        await purger.PurgeAsync(null, "test", ResourceCachePurgeScope.All);

        cache.Verify(item => item.GetImplementation(It.IsAny<ResourceCacheType>()), Times.Never);
    }

    [Fact]
    public async Task PurgeAsync_WhenDeleteThrows_SwallowsTheException()
    {
        var (purger, _, implementation) = BuildPurger(ResourceCacheType.Redis);

        implementation
            .Setup(item => item.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));

        // The caller is already handling a failed message; cleanup failure must not add another.
        await purger.PurgeAsync(BuildValue(ResourceCacheType.Redis), "test", ResourceCachePurgeScope.All);
    }

    private static (ResourceCachePurger, Mock<IResourceCache>, Mock<IResourceCache>) BuildPurger(ResourceCacheType cacheType)
    {
        var implementation = new Mock<IResourceCache>();
        var cache = new Mock<IResourceCache>();
        cache.Setup(item => item.GetImplementation(cacheType)).Returns(implementation.Object);

        var purger = new ResourceCachePurger(cache.Object, Mock.Of<ILogger<ResourceCachePurger>>());

        return (purger, cache, implementation);
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
