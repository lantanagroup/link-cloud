using FluentAssertions;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services.ResourceCache;
using LantanaGroup.Link.Shared.Application.Models.ResourceCache;
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
        _redis.Setup(c => c.GetDurableResourceCountAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(DurableResourceCount.Of(9));

        var whole = Enumerable.Range(0, 9).Select(i => (DomainResource)new Patient { Id = i.ToString() }).ToList();
        _abs.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(whole);
        _abs.Setup(c => c.GetResourceTypeByCacheKey(CacheKey)).Returns(ResourceType.Patient);

        var result = await CreateSut().GetAsync(CacheKey);

        result.Should().HaveCount(9);
        _metrics.Verify(m => m.RecordRead(ResourceCacheOutcomes.Fallback, It.IsAny<string?>(), It.IsAny<double>()), Times.Once);
        _metrics.Verify(m => m.RecordRead(ResourceCacheOutcomes.Hit, It.IsAny<string?>(), It.IsAny<double>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_cache_entry_matching_durable_storage_is_served_from_the_cache()
    {
        _redis.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(Resources());
        _redis.Setup(c => c.GetDurableResourceCountAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(DurableResourceCount.Of(1));

        await CreateSut().GetAsync(CacheKey);

        _metrics.Verify(m => m.RecordRead(ResourceCacheOutcomes.Hit, It.IsAny<string?>(), It.IsAny<double>()), Times.Once);
        _abs.Verify(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_cache_entry_with_no_recorded_count_is_trusted()
    {
        // No count means no durable write has landed for this key, so durable storage has nothing more
        // to offer. Falling back would turn a usable entry into an empty read.
        _redis.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(Resources());
        _redis.Setup(c => c.GetDurableResourceCountAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(DurableResourceCount.NotRecorded);

        await CreateSut().GetAsync(CacheKey);

        _metrics.Verify(m => m.RecordRead(ResourceCacheOutcomes.Hit, It.IsAny<string?>(), It.IsAny<double>()), Times.Once);
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
    public async Task ReplaceResourcesAsync_drains_in_flight_writes_before_writing_either_store()
    {
        var order = new List<string>();
        var resources = new List<DomainResource> { new Encounter { Id = "enc-org" } };

        _writer
            .Setup(w => w.CancelAndDrainAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("drain"))
            .Returns(Task.CompletedTask);
        _abs
            .Setup(c => c.ReplaceResourcesAsync(CacheKey, resources, ResourceType.Encounter, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("durable"))
            .Returns(Task.CompletedTask);
        _redis
            .Setup(c => c.ReplaceResourcesAsync(CacheKey, resources, ResourceType.Encounter, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("cache"))
            .Returns(Task.CompletedTask);

        await CreateSut().ReplaceResourcesAsync(CacheKey, resources, ResourceType.Encounter);

        order.Should().Equal("drain", "durable", "cache");
    }

    [Fact]
    public async Task ReplaceResourcesAsync_does_not_merely_cancel_the_key()
    {
        var resources = new List<DomainResource> { new Encounter { Id = "enc-org" } };

        _writer
            .Setup(w => w.CancelAndDrainAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateSut().ReplaceResourcesAsync(CacheKey, resources, ResourceType.Encounter);

        // Cancel returns while a write is still executing. That write finishes afterwards, sees the
        // key was cancelled, and deletes it from durable storage -- taking the replacement with it,
        // because the replacement was written first. Only a drain orders them correctly.
        _writer.Verify(w => w.Cancel(It.IsAny<IEnumerable<string>>()), Times.Never);
        _writer.Verify(
            w => w.CancelAndDrainAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ----- read parity with MeasureEval (same instrument, same conditions) -----
    //
    // These mirror Java's ResourceCacheReaderTest case for case, so the two matrices can be lined up
    // by eye. The shared rule they both keep: cache.fallback.reason rides every fallback and every
    // empty read, never a hit, and is omitted rather than recorded empty.

    private void GivenCache(params DomainResource[] resources) =>
        _redis.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync([.. resources]);

    private void GivenCacheUnavailable() =>
        _redis.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache down"));

    private void GivenDurableCount(DurableResourceCount count) =>
        _redis.Setup(c => c.GetDurableResourceCountAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(count);

    private void GivenDurable(params DomainResource[] resources) =>
        _abs.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync([.. resources]);

    private void ThenRead(string outcome, string? reason) =>
        _metrics.Verify(m => m.RecordRead(outcome, reason, It.IsAny<double>()), Times.Once);

    [Fact]
    public async Task metrics_redisHit_recordsHit_withNoFallbackReason()
    {
        GivenCache(new Patient { Id = "1" });
        GivenDurableCount(DurableResourceCount.Of(1));

        await CreateSut().GetAsync(CacheKey);

        ThenRead(ResourceCacheOutcomes.Hit, null);
        _metrics.Verify(m => m.IncrementDurableCountReadFailure(), Times.Never);
    }

    [Fact]
    public async Task metrics_redisHitExceedingTheDurableCount_recordsHit()
    {
        GivenCache(new Patient { Id = "1" }, new Patient { Id = "2" });
        GivenDurableCount(DurableResourceCount.Of(1));

        await CreateSut().GetAsync(CacheKey);

        ThenRead(ResourceCacheOutcomes.Hit, null);
    }

    [Fact]
    public async Task metrics_noRecordedCount_recordsHit_andIsNotAFailure()
    {
        GivenCache(new Patient { Id = "1" });
        GivenDurableCount(DurableResourceCount.NotRecorded);

        await CreateSut().GetAsync(CacheKey);

        // Nothing has completed a durable write for this key, so there is nothing to disagree with.
        ThenRead(ResourceCacheOutcomes.Hit, null);
        _metrics.Verify(m => m.IncrementDurableCountReadFailure(), Times.Never);
    }

    [Fact]
    public async Task metrics_redisMiss_recordsFallback_withMissReason()
    {
        GivenCache();
        GivenDurable(new Patient { Id = "1" });

        await CreateSut().GetAsync(CacheKey);

        ThenRead(ResourceCacheOutcomes.Fallback, ResourceCacheFallbackReasons.Miss);
    }

    [Fact]
    public async Task metrics_partialEntry_recordsFallback_withPartialReason()
    {
        GivenCache(new Patient { Id = "1" });
        GivenDurableCount(DurableResourceCount.Of(9));
        GivenDurable(new Patient { Id = "1" }, new Patient { Id = "2" });

        await CreateSut().GetAsync(CacheKey);

        ThenRead(ResourceCacheOutcomes.Fallback, ResourceCacheFallbackReasons.Partial);
    }

    [Fact]
    public async Task metrics_redisUnavailable_recordsFallback_withUnavailableReason()
    {
        GivenCacheUnavailable();
        GivenDurable(new Patient { Id = "1" });

        await CreateSut().GetAsync(CacheKey);

        ThenRead(ResourceCacheOutcomes.Fallback, ResourceCacheFallbackReasons.Unavailable);
    }

    [Fact]
    public async Task metrics_bothStoresEmpty_recordsEmpty_withTheReasonRedisMissed()
    {
        GivenCache();
        GivenDurable();

        await CreateSut().GetAsync(CacheKey);

        ThenRead(ResourceCacheOutcomes.Empty, ResourceCacheFallbackReasons.Miss);
    }

    [Fact]
    public async Task metrics_partialEntryAndDurableEmpty_recordsEmpty_withPartialReason()
    {
        GivenCache(new Patient { Id = "1" });
        GivenDurableCount(DurableResourceCount.Of(9));
        GivenDurable();

        await CreateSut().GetAsync(CacheKey);

        ThenRead(ResourceCacheOutcomes.Empty, ResourceCacheFallbackReasons.Partial);
    }

    [Fact]
    public async Task metrics_redisUnavailableAndDurableEmpty_recordsEmpty_withUnavailableReason()
    {
        GivenCacheUnavailable();
        GivenDurable();

        await CreateSut().GetAsync(CacheKey);

        // The combination worth alerting on: an empty answer that nothing should trust, as against an
        // empty answer after a plain miss, which is routine. Only the reason separates them.
        ThenRead(ResourceCacheOutcomes.Empty, ResourceCacheFallbackReasons.Unavailable);
    }

    [Fact]
    public async Task metrics_unusableDurableCount_isCounted_andTheHitIsStillServed()
    {
        GivenCache(new Patient { Id = "1" });
        GivenDurableCount(DurableResourceCount.Unusable);

        var resources = await CreateSut().GetAsync(CacheKey);

        resources.Should().HaveCount(1);
        ThenRead(ResourceCacheOutcomes.Hit, null);
        _metrics.Verify(m => m.IncrementDurableCountReadFailure(), Times.Once);
    }

    [Fact]
    public async Task metrics_durableCountReadThrowing_isCounted_andTheHitIsStillServed()
    {
        GivenCache(new Patient { Id = "1" });
        _redis.Setup(c => c.GetDurableResourceCountAsync(CacheKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache down"));

        var resources = await CreateSut().GetAsync(CacheKey);

        // The other way .NET can fail to use the count. Java reaches this only via an unusable value,
        // because its count rides the same round trip as the resources; the counter means the same
        // thing either way -- an entry served without the partial-entry check.
        resources.Should().HaveCount(1);
        ThenRead(ResourceCacheOutcomes.Hit, null);
        _metrics.Verify(m => m.IncrementDurableCountReadFailure(), Times.Once);
    }

    [Fact]
    public async Task metrics_durableStorageThrowing_recordsNothing_andStillPropagates()
    {
        GivenCache();
        _abs.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("blob down"));

        var act = () => CreateSut().GetAsync(CacheKey);

        // Matches Java: the read failed, so it is not reported as any outcome. The caller retries.
        await act.Should().ThrowAsync<InvalidOperationException>();
        _metrics.Verify(m => m.RecordRead(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<double>()), Times.Never);
    }

    [Fact]
    public async Task metrics_cancellationDuringTheCacheRead_recordsNothing()
    {
        _redis.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => CreateSut().GetAsync(CacheKey);

        // Cancellation is not a cache outcome, and the catch filters exist to let it through.
        await act.Should().ThrowAsync<OperationCanceledException>();
        _metrics.Verify(m => m.RecordRead(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<double>()), Times.Never);
    }

    [Fact]
    public async Task metrics_cancellationDuringTheCountRead_recordsNothing_andIsNotAFailure()
    {
        GivenCache(new Patient { Id = "1" });
        _redis.Setup(c => c.GetDurableResourceCountAsync(CacheKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => CreateSut().GetAsync(CacheKey);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _metrics.Verify(m => m.RecordRead(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<double>()), Times.Never);
        _metrics.Verify(m => m.IncrementDurableCountReadFailure(), Times.Never);
    }

    [Fact]
    public async Task IsEntryCompleteAsync_withAnUnusableCount_trustsTheEntry_andCountsNothing()
    {
        _redis.Setup(c => c.GetResourceCountAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(3);
        GivenDurableCount(DurableResourceCount.Unusable);

        // Only a partial entry is incomplete. An unusable count serves the entry, as it did when the
        // shared helper returned a bool -- and this is not a read, so it must not touch the counter.
        (await CreateSut().IsEntryCompleteAsync(CacheKey)).Should().BeTrue();
        _metrics.Verify(m => m.IncrementDurableCountReadFailure(), Times.Never);
    }

    [Fact]
    public async Task IsEntryCompleteAsync_is_false_when_the_entry_holds_fewer_than_durable_storage()
    {
        _redis.Setup(c => c.GetResourceCountAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(3);
        _redis.Setup(c => c.GetDurableResourceCountAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(DurableResourceCount.Of(10));

        (await CreateSut().IsEntryCompleteAsync(CacheKey)).Should().BeFalse();
    }

    [Fact]
    public async Task IsEntryCompleteAsync_is_true_when_the_entry_holds_the_whole_record()
    {
        _redis.Setup(c => c.GetResourceCountAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(10);
        _redis.Setup(c => c.GetDurableResourceCountAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(DurableResourceCount.Of(10));

        (await CreateSut().IsEntryCompleteAsync(CacheKey)).Should().BeTrue();

        // The point of asking: the caller skips a read that would deserialize the whole entry.
        _abs.Verify(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IsEntryCompleteAsync_is_false_when_nothing_is_cached()
    {
        _redis.Setup(c => c.GetResourceCountAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        // An evicted entry is exactly the case a caller asks about, so "nothing cached" must not read
        // as "nothing missing".
        (await CreateSut().IsEntryCompleteAsync(CacheKey)).Should().BeFalse();
    }

    [Fact]
    public async Task IsEntryCompleteAsync_is_false_when_the_cache_cannot_be_reached()
    {
        _redis
            .Setup(c => c.GetResourceCountAsync(CacheKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache down"));

        // Not knowing has to cost a read-through, not a wrong answer.
        (await CreateSut().IsEntryCompleteAsync(CacheKey)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_leaves_durable_storage_alone_when_the_cache_delete_fails()
    {
        var keys = new List<string> { CacheKey };

        _redis
            .Setup(c => c.DeleteAsync(keys, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache down"));

        var act = () => CreateSut().DeleteAsync(keys);

        // Deleting the blobs under surviving cache entries would let a redelivered ResourcesAcquired
        // copy one key and find a later, evicted one empty, and dead-letter it.
        await act.Should().ThrowAsync<InvalidOperationException>();
        _abs.Verify(c => c.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()), Times.Never);
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

        _metrics.Verify(m => m.RecordRead(ResourceCacheOutcomes.Hit, It.IsAny<string?>(), It.IsAny<double>()), Times.Once);
        _metrics.Verify(m => m.RecordRead(ResourceCacheOutcomes.Fallback, It.IsAny<string?>(), It.IsAny<double>()), Times.Once);
    }

    [Fact]
    public async Task A_read_that_finds_nothing_anywhere_is_reported_as_empty()
    {
        _redis.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _abs.Setup(c => c.GetAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await CreateSut().GetAsync(CacheKey);

        _metrics.Verify(m => m.RecordRead(ResourceCacheOutcomes.Empty, It.IsAny<string?>(), It.IsAny<double>()), Times.Once);
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
