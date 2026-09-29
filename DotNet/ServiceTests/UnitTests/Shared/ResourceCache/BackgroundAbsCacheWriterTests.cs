using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Services.ResourceCache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Concurrent;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared.ResourceCache;

[Trait("Category", "UnitTests")]
public class BackgroundAbsCacheWriterTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly RecordingAbsCache _abs = new();
    private BackgroundAbsCacheWriter _writer = null!;

    public async Task InitializeAsync()
    {
        _writer = CreateWriter();
        await _writer.StartAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        // Release anything a failing test left blocked, and bound the stop regardless. Without this a
        // single assertion failure turns into a hung test host rather than a red test.
        _abs.ReleaseAll();

        using var cts = new CancellationTokenSource(Timeout);
        await _writer.StopAsync(cts.Token);
    }

    [Fact]
    public async Task EnqueueAsync_QueuedWrite_ReachesDurableStorage()
    {
        await _writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);

        await _writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout);

        Assert.Single(_abs.Writes);
        Assert.Equal("corr:Patient", _abs.Writes[0].CacheKey);
    }

    [Fact]
    public async Task EnqueueAsync_EmptyResourceList_WritesNothing()
    {
        await _writer.EnqueueAsync("corr:Patient", [], ResourceType.Patient);

        await _writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout);

        Assert.Empty(_abs.Writes);
    }

    [Fact]
    public async Task WaitForDurableAsync_PendingWrite_DoesNotCompleteUntilWritten()
    {
        _abs.BlockNextWrite();

        await _writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);
        var wait = _writer.WaitForDurableAsync(["corr:Patient"]);

        await _abs.WaitUntilBlocked().WaitAsync(Timeout);
        Assert.False(wait.IsCompleted);

        _abs.ReleaseBlockedWrite();
        await wait.WaitAsync(Timeout);
    }

    [Fact]
    public async Task WaitForDurableAsync_UnrelatedKeyPending_ReturnsImmediately()
    {
        _abs.BlockNextWrite();
        await _writer.EnqueueAsync("other:Patient", Resources("Patient/1"), ResourceType.Patient);
        await _abs.WaitUntilBlocked().WaitAsync(Timeout);

        // The barrier must be scoped to the keys it was given, or one log would wait on another's.
        await _writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout);

        _abs.ReleaseBlockedWrite();
    }

    [Fact]
    public async Task WaitForDurableAsync_WriteFailsEveryAttempt_Throws()
    {
        _abs.FailKey("corr:Patient");

        await _writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);

        var ex = await Assert.ThrowsAsync<ResourceCacheDurabilityException>(
            () => _writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout));
        Assert.Contains("corr:Patient", ex.Message);
    }

    [Fact]
    public async Task WaitForDurableAsync_FailureThenSuccess_DoesNotThrow()
    {
        _abs.FailKey("corr:Patient");
        await _writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);
        await Assert.ThrowsAsync<ResourceCacheDurabilityException>(
            () => _writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout));

        _abs.ClearFailures();
        await _writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);

        await _writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout);
    }

    [Fact]
    public async Task WriteWithRetry_TransientFailure_RetriesAndSucceeds()
    {
        _abs.FailKeyTimes("corr:Patient", 2);

        await _writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);

        await _writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout);
        Assert.Equal(3, _abs.AttemptsFor("corr:Patient"));
    }

    [Fact]
    public async Task EnqueueAsync_SameKeyConcurrently_SerializesWrites()
    {
        // The blob write reads the key's id list before appending, so two overlapping writes to one
        // key would each miss the other's resources.
        for (var i = 0; i < 8; i++)
        {
            await _writer.EnqueueAsync("corr:Patient", Resources($"Patient/{i}"), ResourceType.Patient);
        }

        await _writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout);

        Assert.Equal(8, _abs.Writes.Count);
        Assert.Equal(1, _abs.MaxConcurrentWritesFor("corr:Patient"));
    }

    [Fact]
    public async Task EnqueueAsync_DifferentKeys_WriteConcurrently()
    {
        var writer = CreateWriter(settings => settings.MaxConcurrency = 4);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            _abs.HoldUntil(4);

            for (var i = 0; i < 4; i++)
            {
                await writer.EnqueueAsync($"corr{i}:Patient", Resources("Patient/1"), ResourceType.Patient);
            }

            // Completes only if all four are in flight at once.
            await _abs.WaitForConcurrency(4).WaitAsync(Timeout);
        }
        finally
        {
            _abs.ReleaseHold();
            await writer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Cancel_QueuedWrite_IsNotWritten()
    {
        // One consumer, held on a blocking write, guarantees the cancelled item is still sitting in
        // the channel when Cancel runs. With more than one it is a race whether a consumer has
        // already picked it up, and the test would pass or fail on timing.
        var writer = CreateWriter(settings => settings.MaxConcurrency = 1);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            _abs.BlockNextWrite();
            await writer.EnqueueAsync("blocker:Patient", Resources("Patient/0"), ResourceType.Patient);
            await _abs.WaitUntilBlocked().WaitAsync(Timeout);

            await writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);
            writer.Cancel(["corr:Patient"]);

            _abs.ReleaseBlockedWrite();
            await writer.WaitForDurableAsync(["blocker:Patient", "corr:Patient"]).WaitAsync(Timeout);

            Assert.Contains(_abs.Writes, write => write.CacheKey == "blocker:Patient");
            Assert.DoesNotContain(_abs.Writes, write => write.CacheKey == "corr:Patient");
        }
        finally
        {
            await writer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Cancel_WriteHeldBehindAnotherWriteToTheSameKey_IsNotWritten()
    {
        // The window the queued-item check alone does not cover. Writes to one key are serialized,
        // so a second write can be past that check and parked on the key's write lock while Cancel
        // and the delete that follows it both run. Writing after that puts the deleted key back in
        // blob storage, where nothing expires it.
        var writer = CreateWriter(settings => settings.MaxConcurrency = 3);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            _abs.BlockWritesFor("corr:Patient");

            await writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);
            await _abs.WaitUntilBlocked().WaitAsync(Timeout);

            // Dequeued by a second worker, which then parks on the write lock the first one holds.
            await writer.EnqueueAsync("corr:Patient", Resources("Patient/2"), ResourceType.Patient);

            // Enqueued after it, and not gated. The channel is FIFO, so this one being written
            // proves the one before it was handed to a worker - otherwise the test would pass on
            // the queued-item check and never reach the one being fixed here.
            await writer.EnqueueAsync("probe:Patient", Resources("Patient/3"), ResourceType.Patient);
            await writer.WaitForDurableAsync(["probe:Patient"]).WaitAsync(Timeout);

            // What HybridResourceCache.DeleteAsync does immediately before deleting both stores.
            writer.Cancel(["corr:Patient"]);

            _abs.ReleaseBlockedWrite();
            await writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout);

            // The in-flight write is allowed to land; the delete that follows Cancel removes it.
            // The parked one must not run at all, because it would land after that delete.
            Assert.Single(_abs.Writes.Where(write => write.CacheKey == "corr:Patient"));
        }
        finally
        {
            // DisposeAsync releases the fake's gates; doing it here too over-releases its semaphore.
            await writer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ExhaustedWrite_IsNotMaskedByALaterWriteToTheSameKey()
    {
        // Normalization writes this key once per resource type, so several writes for one key in a
        // single pass is the normal case, not an edge one. Each carries different resources, which
        // is why a later one landing says nothing about an earlier one that never did.
        //
        // Exactly MaxRetryAttempts failures, so the first write exhausts and the second succeeds.
        // Writes to one key are serialized, so the Patient batch consumes all three.
        _abs.FailKeyTimes("corr:Patient", 3);

        await _writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);
        await _writer.EnqueueAsync("corr:Patient", Resources("Encounter/1"), ResourceType.Encounter);

        // Both are in the same cycle and the caller waits once, after both. The Encounter batch is
        // durable and the Patient batch is not, so the key is not durable.
        await Assert.ThrowsAsync<ResourceCacheDurabilityException>(
            () => _writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout));

        Assert.Single(_abs.Writes.Where(write => write.CacheKey == "corr:Patient"));
    }

    [Fact]
    public async Task ExhaustedWrite_ReportedOnce_ThenTheKeyCanRecover()
    {
        // The caller fails its work and the message is redelivered, which rewrites the key from
        // the start. A failure that outlived its report would fail that attempt too, and the
        // message could never recover from a transient outage.
        _abs.FailKey("corr:Patient");
        await _writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);

        await Assert.ThrowsAsync<ResourceCacheDurabilityException>(
            () => _writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout));

        _abs.ClearFailures();
        await _writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);

        await _writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout);
    }

    [Fact]
    public async Task Cancel_RecordedFailure_IsCleared()
    {
        _abs.FailKey("corr:Patient");
        await _writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);
        await Assert.ThrowsAsync<ResourceCacheDurabilityException>(
            () => _writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout));

        _writer.Cancel(["corr:Patient"]);

        // The key is being deleted, so nothing is owed a durability answer for it any more.
        await _writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout);
    }

    [Fact]
    public async Task StopAsync_QueuedWrites_DrainsBeforeReturning()
    {
        var writer = CreateWriter();
        await writer.StartAsync(CancellationToken.None);

        for (var i = 0; i < 20; i++)
        {
            await writer.EnqueueAsync($"corr{i}:Patient", Resources("Patient/1"), ResourceType.Patient);
        }

        await writer.StopAsync(CancellationToken.None).WaitAsync(Timeout);

        Assert.Equal(20, _abs.Writes.Count);
    }

    [Fact]
    public async Task DrainedKey_IsNotRetainedForever()
    {
        for (var i = 0; i < 50; i++)
        {
            await _writer.EnqueueAsync($"corr{i}:Patient", Resources("Patient/1"), ResourceType.Patient);
            await _writer.WaitForDurableAsync([$"corr{i}:Patient"]).WaitAsync(Timeout);
        }

        // One entry per cache key retained for the life of the process would be a leak: a worker
        // handles millions of keys per run.
        Assert.Equal(0, _writer.QueueDepth);
        Assert.Equal(0, _writer.TrackedKeyCount);
    }

    // -------------------------------------------------------------------------

    private BackgroundAbsCacheWriter CreateWriter(Action<ResourceCacheAbsWriterSettings>? configure = null)
    {
        var settings = new ResourceCacheSettings
        {
            AbsWriter = new ResourceCacheAbsWriterSettings
            {
                QueueCapacity = 100,
                MaxConcurrency = 2,
                MaxRetryAttempts = 3,
                RetryBaseDelayMilliseconds = 1,
                DrainTimeoutSeconds = 10
            }
        };
        configure?.Invoke(settings.AbsWriter);

        return new BackgroundAbsCacheWriter(
            _abs,
            Options.Create(settings),
            Mock.Of<IResourceCacheMetrics>(),
            Mock.Of<ILogger<BackgroundAbsCacheWriter>>());
    }

    private static List<DomainResource> Resources(params string[] references)
    {
        return references
            .Select(reference => (DomainResource)new Patient { Id = reference.Split('/')[1] })
            .ToList();
    }

    /// <summary>
    /// A stand-in for the blob cache that records what it was asked to write and can be made to
    /// block, fail, or fail a fixed number of times.
    /// </summary>
    private sealed class RecordingAbsCache : IResourceCache
    {
        private readonly ConcurrentQueue<(string CacheKey, int Count)> _writes = new();
        private readonly ConcurrentDictionary<string, int> _attempts = new();
        private readonly ConcurrentDictionary<string, int> _failuresRemaining = new();
        private readonly ConcurrentDictionary<string, int> _peakConcurrency = new();
        private readonly ConcurrentDictionary<string, int> _liveWrites = new();
        private readonly HashSet<string> _alwaysFail = [];
        private readonly object _gate = new();

        private readonly SemaphoreSlim _writeEntered = new(0);
        private readonly SemaphoreSlim _writeRelease = new(0);
        private volatile bool _gateWrites;
        private volatile string? _gatedKey;

        private TaskCompletionSource? _holdRelease;
        private TaskCompletionSource? _concurrencyReached;
        private int _holdTarget;
        private int _holdArrived;

        public IReadOnlyList<(string CacheKey, int Count)> Writes => _writes.ToList();

        public int AttemptsFor(string cacheKey) => _attempts.TryGetValue(cacheKey, out var n) ? n : 0;

        public int MaxConcurrentWritesFor(string cacheKey) =>
            _peakConcurrency.TryGetValue(cacheKey, out var n) ? n : 0;

        public void FailKey(string cacheKey)
        {
            lock (_gate)
            {
                _alwaysFail.Add(cacheKey);
            }
        }

        public void FailKeyTimes(string cacheKey, int times) => _failuresRemaining[cacheKey] = times;

        public void ClearFailures()
        {
            lock (_gate)
            {
                _alwaysFail.Clear();
            }

            _failuresRemaining.Clear();
        }

        /// <summary>
        /// Makes writes park until <see cref="ReleaseBlockedWrite"/>, so a test can observe the state
        /// of the world while a write is genuinely in flight.
        /// </summary>
        public void BlockNextWrite() => _gateWrites = true;

        /// <summary>
        /// Parks writes for one key only, leaving every other key free to complete. Lets a test
        /// hold a key's write in flight and still observe the queue moving past it.
        /// </summary>
        public void BlockWritesFor(string cacheKey) => _gatedKey = cacheKey;

        /// <summary>Completes once a write has entered and parked.</summary>
        public Task WaitUntilBlocked() => _writeEntered.WaitAsync();

        public void ReleaseBlockedWrite()
        {
            _gateWrites = false;
            _gatedKey = null;
            _writeRelease.Release(int.MaxValue / 2);
        }

        public void HoldUntil(int concurrentWrites)
        {
            _holdTarget = concurrentWrites;
            _holdArrived = 0;
            _holdRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _concurrencyReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Task WaitForConcurrency(int _) => _concurrencyReached!.Task;

        public void ReleaseHold() => _holdRelease?.TrySetResult();

        /// <summary>
        /// Unblocks every gate, so cleanup after a failed assertion cannot deadlock.
        /// </summary>
        public void ReleaseAll()
        {
            ReleaseBlockedWrite();
            _holdRelease?.TrySetResult();
            _concurrencyReached?.TrySetResult();
        }

        public async Task UpdateCorrelationCacheAsync(
            string correlationId,
            List<DomainResource> resources,
            ResourceType resourceType,
            CancellationToken cancellationToken = default)
        {
            _attempts.AddOrUpdate(correlationId, 1, (_, n) => n + 1);

            var live = _liveWrites.AddOrUpdate(correlationId, 1, (_, n) => n + 1);
            _peakConcurrency.AddOrUpdate(correlationId, live, (_, peak) => Math.Max(peak, live));

            try
            {
                if (_holdRelease != null)
                {
                    if (Interlocked.Increment(ref _holdArrived) >= _holdTarget)
                    {
                        _concurrencyReached?.TrySetResult();
                    }

                    await _holdRelease.Task;
                }

                if (_gateWrites || _gatedKey == correlationId)
                {
                    _writeEntered.Release();
                    await _writeRelease.WaitAsync();
                }

                lock (_gate)
                {
                    if (_alwaysFail.Contains(correlationId))
                    {
                        throw new InvalidOperationException($"Injected failure for '{correlationId}'.");
                    }
                }

                if (_failuresRemaining.TryGetValue(correlationId, out var remaining) && remaining > 0)
                {
                    _failuresRemaining[correlationId] = remaining - 1;
                    throw new InvalidOperationException($"Injected transient failure for '{correlationId}'.");
                }

                _writes.Enqueue((correlationId, resources.Count));
            }
            finally
            {
                _liveWrites.AddOrUpdate(correlationId, 0, (_, n) => n - 1);
            }
        }

        public Task<List<DomainResource>> GetAsync(string cacheKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<DomainResource>());

        public Task DeleteAsync(List<string> cacheKeys, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> HasResourcesAsync(string cacheKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public ResourceType GetResourceTypeByCacheKey(string cacheKey) => ResourceType.Patient;

        public Task WaitForDurableAsync(string correlationId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
