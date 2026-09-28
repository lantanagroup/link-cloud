using FluentAssertions;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Services.ResourceCache;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared.ResourceCache;

[Trait("Category", "UnitTests")]
public class HybridResourceCacheTests
{
    private const string CacheKey = "corr-1:Patient";

    private readonly Mock<IResourceCache> _redis = new();
    private readonly Mock<IResourceCache> _abs = new();
    private readonly Mock<IBackgroundAbsCacheWriter> _writer = new();

    private HybridResourceCache CreateSut() =>
        new(_redis.Object, _abs.Object, _writer.Object, Mock.Of<ILogger<HybridResourceCache>>());

    private static List<DomainResource> Resources() => [new Patient { Id = "1" }];

    [Fact]
    public async Task UpdateCorrelationCacheAsync_writes_the_cache_inline_and_queues_the_durable_write()
    {
        await CreateSut().UpdateCorrelationCacheAsync(CacheKey, Resources(), ResourceType.Patient);

        _redis.Verify(c => c.UpdateCorrelationCacheAsync(CacheKey, It.IsAny<List<DomainResource>>(), ResourceType.Patient, It.IsAny<CancellationToken>()), Times.Once);
        _writer.Verify(w => w.EnqueueAsync(CacheKey, It.IsAny<List<DomainResource>>(), ResourceType.Patient, It.IsAny<CancellationToken>()), Times.Once);

        // The durable write is queued and never awaited here; that is the point of the design.
        _abs.Verify(c => c.UpdateCorrelationCacheAsync(It.IsAny<string>(), It.IsAny<List<DomainResource>>(), It.IsAny<ResourceType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCorrelationCacheAsync_with_no_resources_does_nothing()
    {
        await CreateSut().UpdateCorrelationCacheAsync(CacheKey, [], ResourceType.Patient);

        _redis.VerifyNoOtherCalls();
        _writer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateCorrelationCacheAsync_drops_the_entry_when_the_cache_write_fails()
    {
        _redis
            .Setup(c => c.UpdateCorrelationCacheAsync(CacheKey, It.IsAny<List<DomainResource>>(), ResourceType.Patient, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache down"));

        await CreateSut().UpdateCorrelationCacheAsync(CacheKey, Resources(), ResourceType.Patient);

        // A half-written entry would win over the complete durable copy, because reads prefer the
        // cache. The durable write still has to be queued.
        _redis.Verify(c => c.DeleteAsync(It.Is<List<string>>(k => k.Contains(CacheKey)), It.IsAny<CancellationToken>()), Times.Once);
        _writer.Verify(w => w.EnqueueAsync(CacheKey, It.IsAny<List<DomainResource>>(), ResourceType.Patient, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_serves_a_cache_hit_without_touching_durable_storage()
    {
        _redis.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(Resources());

        var result = await CreateSut().GetAsync(CacheKey);

        result.Should().HaveCount(1);
        _abs.Verify(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_falls_back_to_durable_storage_and_repopulates_the_cache()
    {
        _redis.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _abs.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(Resources());
        _abs.Setup(c => c.GetResourceTypeByCacheKey(CacheKey)).Returns(ResourceType.Patient);

        var result = await CreateSut().GetAsync(CacheKey);

        result.Should().HaveCount(1);
        _redis.Verify(c => c.UpdateCorrelationCacheAsync(CacheKey, It.IsAny<List<DomainResource>>(), ResourceType.Patient, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_falls_back_to_durable_storage_when_the_cache_is_unreachable()
    {
        _redis
            .Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache down"));
        _abs.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(Resources());
        _abs.Setup(c => c.GetResourceTypeByCacheKey(CacheKey)).Returns(ResourceType.Patient);

        // An unreachable cache is a slower read, never a failure.
        var result = await CreateSut().GetAsync(CacheKey);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAsync_returns_empty_without_repopulating_when_neither_store_holds_the_key()
    {
        _redis.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _abs.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await CreateSut().GetAsync(CacheKey);

        result.Should().BeEmpty();
        _redis.Verify(c => c.UpdateCorrelationCacheAsync(It.IsAny<string>(), It.IsAny<List<DomainResource>>(), It.IsAny<ResourceType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HasResourcesAsync_checks_durable_storage_when_the_cache_has_nothing()
    {
        _redis.Setup(c => c.HasResourcesAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _abs.Setup(c => c.HasResourcesAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        (await CreateSut().HasResourcesAsync(CacheKey)).Should().BeTrue();
    }

    [Fact]
    public async Task HasResourcesAsync_checks_durable_storage_when_the_cache_is_unreachable()
    {
        _redis
            .Setup(c => c.HasResourcesAsync(CacheKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache down"));
        _abs.Setup(c => c.HasResourcesAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        (await CreateSut().HasResourcesAsync(CacheKey)).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_cancels_queued_writes_before_deleting_from_both_stores()
    {
        var order = new List<string>();
        var keys = new List<string> { CacheKey };

        _writer.Setup(w => w.Cancel(keys)).Callback(() => order.Add("cancel"));
        _redis
            .Setup(c => c.DeleteAsync(keys, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("cache"))
            .Returns(Task.CompletedTask);
        _abs
            .Setup(c => c.DeleteAsync(keys, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("durable"))
            .Returns(Task.CompletedTask);

        await CreateSut().DeleteAsync(keys);

        // Cancel has to come first, or a write still on the queue recreates what was just removed.
        order.Should().Equal("cancel", "cache", "durable");
    }

    [Fact]
    public async Task DeleteAsync_still_deletes_from_durable_storage_when_the_cache_delete_fails()
    {
        var keys = new List<string> { CacheKey };

        _redis
            .Setup(c => c.DeleteAsync(keys, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache down"));

        await CreateSut().DeleteAsync(keys);

        _abs.Verify(c => c.DeleteAsync(keys, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WaitForDurableAsync_waits_on_the_correlations_queued_writes()
    {
        await CreateSut().WaitForDurableAsync("corr-1");

        _writer.Verify(w => w.WaitForCorrelationAsync("corr-1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
