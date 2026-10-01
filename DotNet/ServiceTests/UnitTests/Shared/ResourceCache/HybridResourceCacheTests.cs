using FluentAssertions;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
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
    private readonly Mock<IResourceCacheMetrics> _metrics = new();

    private HybridResourceCache CreateSut() =>
        new(_redis.Object, _abs.Object, _writer.Object, _metrics.Object, Mock.Of<ILogger<HybridResourceCache>>());

    private static List<DomainResource> Resources() => [new Patient { Id = "1" }];

    [Fact]
    public async Task AppendResourcesAsync_writes_the_cache_inline_and_queues_the_durable_write()
    {
        await CreateSut().AppendResourcesAsync(CacheKey, Resources(), ResourceType.Patient);

        _redis.Verify(c => c.AppendResourcesAsync(CacheKey, It.IsAny<List<DomainResource>>(), ResourceType.Patient, It.IsAny<CancellationToken>()), Times.Once);
        _writer.Verify(w => w.EnqueueAsync(CacheKey, It.IsAny<List<DomainResource>>(), ResourceType.Patient, It.IsAny<CancellationToken>()), Times.Once);

        // The durable write is queued and never awaited here; that is the point of the design.
        _abs.Verify(c => c.AppendResourcesAsync(It.IsAny<string>(), It.IsAny<List<DomainResource>>(), It.IsAny<ResourceType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AppendResourcesAsync_with_no_resources_does_nothing()
    {
        await CreateSut().AppendResourcesAsync(CacheKey, [], ResourceType.Patient);

        _redis.VerifyNoOtherCalls();
        _writer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AppendResourcesAsync_drops_the_entry_when_the_cache_write_fails()
    {
        _redis
            .Setup(c => c.AppendResourcesAsync(CacheKey, It.IsAny<List<DomainResource>>(), ResourceType.Patient, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache down"));

        await CreateSut().AppendResourcesAsync(CacheKey, Resources(), ResourceType.Patient);

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
        _redis.Verify(c => c.AppendResourcesAsync(CacheKey, It.IsAny<List<DomainResource>>(), ResourceType.Patient, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_falls_back_on_a_correlation_key_and_still_repopulates_the_cache()
    {
        const string correlationKey = "corr-1";

        _redis.Setup(c => c.GetAsync(correlationKey, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _abs.Setup(c => c.GetAsync(correlationKey, It.IsAny<CancellationToken>())).ReturnsAsync(Resources());

        // What the real implementation does with a key that carries no resource type.
        _abs.Setup(c => c.GetResourceTypeByCacheKey(correlationKey))
            .Throws(new Exception("Cache key 'corr-1' does not contain required ':' divider."));

        var result = await CreateSut().GetAsync(correlationKey);

        result.Should().HaveCount(1);

        // Leaving this entry evicted is what lets a supplemental append recreate it holding only
        // that pass's resources, which then reads as complete and shadows the durable copy.
        _redis.Verify(
            c => c.AppendResourcesAsync(correlationKey, It.IsAny<List<DomainResource>>(), It.IsAny<ResourceType>(), It.IsAny<CancellationToken>()),
            Times.Once);
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
        _redis.Verify(c => c.AppendResourcesAsync(It.IsAny<string>(), It.IsAny<List<DomainResource>>(), It.IsAny<ResourceType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_cache_entry_holding_less_than_durable_storage_is_not_served()
    {
        // The entry an eviction plus a partial append leaves behind: non-empty, freshly expiring, and
        // otherwise indistinguishable from the whole record. Only the recorded durable count separates
        // them, which is the whole reason it is recorded.
        _redis.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(Resources());
        _redis.Setup(c => c.GetDurableResourceCountAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(9);

        var whole = Enumerable.Range(0, 9).Select(i => (DomainResource)new Patient { Id = i.ToString() }).ToList();
        _abs.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(whole);
        _abs.Setup(c => c.GetResourceTypeByCacheKey(CacheKey)).Returns(ResourceType.Patient);

        var result = await CreateSut().GetAsync(CacheKey);

        result.Should().HaveCount(9);
        _metrics.Verify(m => m.RecordRead(ResourceCacheOutcomes.Fallback, It.IsAny<double>()), Times.Once);
        _metrics.Verify(m => m.RecordRead(ResourceCacheOutcomes.Hit, It.IsAny<double>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_cache_entry_matching_durable_storage_is_served_from_the_cache()
    {
        _redis.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(Resources());
        _redis.Setup(c => c.GetDurableResourceCountAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await CreateSut().GetAsync(CacheKey);

        _metrics.Verify(m => m.RecordRead(ResourceCacheOutcomes.Hit, It.IsAny<double>()), Times.Once);
        _abs.Verify(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_cache_entry_with_no_recorded_count_is_trusted()
    {
        // No count means no durable write has landed for this key, so durable storage has nothing more
        // to offer. Falling back would turn a usable entry into an empty read.
        _redis.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(Resources());
        _redis.Setup(c => c.GetDurableResourceCountAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);

        await CreateSut().GetAsync(CacheKey);

        _metrics.Verify(m => m.RecordRead(ResourceCacheOutcomes.Hit, It.IsAny<double>()), Times.Once);
        _abs.Verify(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_repopulating_the_cache_records_the_durable_count()
    {
        // Without this the restored entry has no count, and the next partial recreation of it would be
        // indistinguishable from whole all over again.
        _redis.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _abs.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(Resources());
        _abs.Setup(c => c.GetResourceTypeByCacheKey(CacheKey)).Returns(ResourceType.Patient);

        await CreateSut().GetAsync(CacheKey);

        _redis.Verify(c => c.SetDurableResourceCountAsync(CacheKey, 1, It.IsAny<CancellationToken>()), Times.Once);
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

    [Fact]
    public async Task A_cache_hit_and_a_fallback_are_reported_as_different_outcomes()
    {
        // The ratio between these two is the number that says whether Redis is earning its place,
        // so recording both as the same outcome would make the whole dashboard meaningless.
        _redis.Setup(c => c.GetAsync("hit-key", It.IsAny<CancellationToken>())).ReturnsAsync(Resources());
        _redis.Setup(c => c.GetAsync("miss-key", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _abs.Setup(c => c.GetAsync("miss-key", It.IsAny<CancellationToken>())).ReturnsAsync(Resources());
        _abs.Setup(c => c.GetResourceTypeByCacheKey("miss-key")).Returns(ResourceType.Patient);

        var sut = CreateSut();
        await sut.GetAsync("hit-key");
        await sut.GetAsync("miss-key");

        _metrics.Verify(m => m.RecordRead(ResourceCacheOutcomes.Hit, It.IsAny<double>()), Times.Once);
        _metrics.Verify(m => m.RecordRead(ResourceCacheOutcomes.Fallback, It.IsAny<double>()), Times.Once);
    }

    [Fact]
    public async Task A_read_that_finds_nothing_anywhere_is_reported_as_empty()
    {
        _redis.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _abs.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await CreateSut().GetAsync(CacheKey);

        _metrics.Verify(m => m.RecordRead(ResourceCacheOutcomes.Empty, It.IsAny<double>()), Times.Once);
    }

    [Fact]
    public async Task A_failed_cache_write_is_reported_as_a_failed_write()
    {
        _redis
            .Setup(c => c.AppendResourcesAsync(CacheKey, It.IsAny<List<DomainResource>>(), ResourceType.Patient, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache down"));

        await CreateSut().AppendResourcesAsync(CacheKey, Resources(), ResourceType.Patient);

        _metrics.Verify(m => m.RecordWrite(ResourceCacheStores.Redis, ResourceCacheOutcomes.Failed, It.IsAny<double>()), Times.Once);
        _metrics.Verify(m => m.RecordWrite(ResourceCacheStores.Redis, ResourceCacheOutcomes.Ok, It.IsAny<double>()), Times.Never);
    }

    [Fact]
    public async Task The_barrier_is_timed_even_when_it_fails()
    {
        // A caller that waited and then failed still paid the time; not recording it would flatter
        // the one measurement this change is judged on.
        _writer
            .Setup(w => w.WaitForCorrelationAsync("corr-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceCacheDurabilityException("not durable"));

        await Assert.ThrowsAsync<ResourceCacheDurabilityException>(() => CreateSut().WaitForDurableAsync("corr-1"));

        _metrics.Verify(m => m.RecordDrainWait(It.IsAny<double>()), Times.Once);
    }
}
