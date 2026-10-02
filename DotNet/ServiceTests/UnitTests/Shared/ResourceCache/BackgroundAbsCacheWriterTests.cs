using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Services.ResourceCache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using System.Collections.Concurrent;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared.ResourceCache;

[Trait("Category", "UnitTests")]
public class BackgroundAbsCacheWriterTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly RecordingAbsCache _abs = new();
    private readonly RecordingAbsCache _redis = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));
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
    public async Task CancelAndDrainAsync_DoesNotReturnWhileAWriteIsStillExecuting()
    {
        // The reason this exists rather than Cancel. A write already executing has passed every
        // generation check, and on finishing it sees the key was cancelled and deletes it from
        // durable storage. A caller that is about to *write* the key -- the encounter strip -- would
        // have its write deleted out from under it. Returning only once nothing is writing makes the
        // caller's write the last one.
        _abs.BlockNextWrite();

        await _writer.EnqueueAsync("corr:Encounter", Resources("Encounter/1"), ResourceType.Encounter);
        await _abs.WaitUntilBlocked().WaitAsync(Timeout);

        var drain = _writer.CancelAndDrainAsync(["corr:Encounter"]);

        Assert.False(drain.IsCompleted);

        _abs.ReleaseBlockedWrite();
        await drain.WaitAsync(Timeout);
    }

    [Fact]
    public async Task CancelAndDrainAsync_UnknownKey_ReturnsImmediately()
    {
        await _writer.CancelAndDrainAsync(["never:Seen"]).WaitAsync(Timeout);
    }

    [Fact]
    public async Task CancelAndDrainAsync_DrainedWriteFailed_DoesNotReportItToTheNextWaiter()
    {
        _abs.FailKey("corr:Encounter");

        await _writer.EnqueueAsync("corr:Encounter", Resources("Encounter/1"), ResourceType.Encounter);

        // The failure describes contents the caller is replacing outright. Leaving it recorded would
        // fail some later waiter over a copy that no longer exists.
        await _writer.CancelAndDrainAsync(["corr:Encounter"]).WaitAsync(Timeout);

        _abs.ClearFailures();
        await _writer.WaitForDurableAsync(["corr:Encounter"]).WaitAsync(Timeout);
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

        var writes = _abs.Writes.Where(write => write.CacheKey == "corr:Patient").ToList();

        // Serialization is the guarantee. The number of blob calls is not: a worker that finds the
        // key busy hands its batch to the holder, and successive hand-offs merge, so eight enqueues
        // land in as few as one write.
        Assert.Equal(1, _abs.MaxConcurrentWritesFor("corr:Patient"));
        Assert.InRange(writes.Count, 1, 8);

        // What merging must never do is lose any of them.
        Assert.Equal(8, writes.Sum(write => write.Count));
    }

    [Fact]
    public async Task BusyKey_DoesNotPinEveryWorker_SoAnotherKeyStillGetsWritten()
    {
        // The measured production shape: Normalization writes the correlation key once per acquired
        // resource type, averaging 4.6 and reaching 11, against a default MaxConcurrency of 8. Every
        // worker that dequeues one of those parks on the key's write lock, so one patient can hold
        // the whole pool while other correlations' work sits undispatched.
        var writer = CreateWriter(settings => settings.MaxConcurrency = 2);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            _abs.BlockWritesFor("busy:Patient");

            await writer.EnqueueAsync("busy:Patient", Resources("Patient/1"), ResourceType.Patient);

            // Worker one is now inside the blocked write, holding the key.
            await _abs.WaitUntilBlocked().WaitAsync(Timeout);

            // Worker two dequeues this, finds the key busy, and must not park on it.
            await writer.EnqueueAsync("busy:Patient", Resources("Patient/2"), ResourceType.Patient);

            // Behind it in the channel and not gated. The channel is FIFO, so this can only be
            // written if worker two moved past the busy key rather than waiting on it.
            await writer.EnqueueAsync("other:Patient", Resources("Patient/3"), ResourceType.Patient);

            await writer.WaitForDurableAsync(["other:Patient"]).WaitAsync(Timeout);

            Assert.Contains(_abs.Writes, write => write.CacheKey == "other:Patient");
        }
        finally
        {
            _abs.ReleaseBlockedWrite();
            await writer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task BusyKey_AtTheMeasuredShape_DoesNotStopTheQueueDraining()
    {
        // The production shape: one correlation writes its key once per acquired resource type,
        // measured at up to 11 against 8 workers. Every one of those used to take a worker.
        var writer = CreateWriter(settings => settings.MaxConcurrency = 2);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            _abs.BlockWritesFor("hot:Patient");

            await writer.EnqueueAsync("hot:Patient", Resources("Patient/0"), ResourceType.Patient);
            await _abs.WaitUntilBlocked().WaitAsync(Timeout);

            var others = new List<string>();
            for (var i = 1; i <= 11; i++)
            {
                await writer.EnqueueAsync("hot:Patient", Resources($"Patient/{i}"), ResourceType.Patient);

                var other = $"other{i}:Patient";
                others.Add(other);
                await writer.EnqueueAsync(other, Resources($"Patient/{i}"), ResourceType.Patient);
            }

            // Every other key is durable while the hot key is still blocked.
            await writer.WaitForDurableAsync(others).WaitAsync(Timeout);
        }
        finally
        {
            _abs.ReleaseBlockedWrite();
            await writer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task HandedOffWrites_MergeIntoOneBlobCall_WithoutLosingResources()
    {
        var writer = CreateWriter(settings => settings.MaxConcurrency = 4);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            _abs.BlockWritesFor("corr:Patient");

            await writer.EnqueueAsync("corr:Patient", Resources("Patient/0"), ResourceType.Patient);
            await _abs.WaitUntilBlocked().WaitAsync(Timeout);

            // Five more while the key is held. They hand off and merge into one follow-up.
            for (var i = 1; i <= 5; i++)
            {
                await writer.EnqueueAsync("corr:Patient", Resources($"Patient/{i}"), ResourceType.Patient);
            }

            _abs.ReleaseBlockedWrite();
            await writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout);

            var writes = _abs.Writes.Where(write => write.CacheKey == "corr:Patient").ToList();

            // The point of the exercise: six enqueues, two blob round trips.
            Assert.Equal(2, writes.Count);
            Assert.Equal(6, writes.Sum(write => write.Count));
        }
        finally
        {
            _abs.ReleaseBlockedWrite();
            await writer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task HandedOffWrite_IsStillCoveredByTheDurabilityBarrier()
    {
        // The donor returns without decrementing Outstanding, so the barrier has to keep waiting on
        // work it no longer owns. If it did not, a caller would advertise a key whose second batch
        // had never reached blob storage.
        var writer = CreateWriter(settings => settings.MaxConcurrency = 2);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            _abs.BlockWritesFor("corr:Patient");

            await writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);
            await _abs.WaitUntilBlocked().WaitAsync(Timeout);
            await writer.EnqueueAsync("corr:Patient", Resources("Patient/2"), ResourceType.Patient);

            var barrier = writer.WaitForDurableAsync(["corr:Patient"]);
            Assert.False(barrier.IsCompleted);

            _abs.ReleaseBlockedWrite();
            await barrier.WaitAsync(Timeout);

            Assert.Equal(2, _abs.Writes.Where(write => write.CacheKey == "corr:Patient").Sum(write => write.Count));
        }
        finally
        {
            _abs.ReleaseBlockedWrite();
            await writer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task CancelAndDrainAsync_WithAHandOffOutstanding_WaitsRatherThanHanging()
    {
        // The hang test. A handed-off item stays counted, so the drain must wait for it -- and the
        // holder must be guaranteed to pick it up, or this never returns.
        var writer = CreateWriter(settings => settings.MaxConcurrency = 2);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            _abs.BlockWritesFor("corr:Patient");

            await writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);
            await _abs.WaitUntilBlocked().WaitAsync(Timeout);
            await writer.EnqueueAsync("corr:Patient", Resources("Patient/2"), ResourceType.Patient);

            var drain = writer.CancelAndDrainAsync(["corr:Patient"]);
            Assert.False(drain.IsCompleted);

            _abs.ReleaseBlockedWrite();
            await drain.WaitAsync(Timeout);
        }
        finally
        {
            _abs.ReleaseBlockedWrite();
            await writer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task WriteEnqueuedAfterACancel_IsNotDiscardedWithTheCancelledHandOff()
    {
        // A cancel bumps the generation but leaves the hand-off attached until the holder collects
        // it. A write arriving in that window is new work -- it passes the generation check -- but it
        // was merged into the stale batch and discarded with it, and its waiter was still told the
        // key was durable. Silent loss reported as success.
        //
        // CancelledHandOff_IsAccountedForRatherThanStranded does not reach this: it enqueues nothing
        // after the cancel, so there is never a new-generation write to lose.
        var writer = CreateWriter(settings => settings.MaxConcurrency = 3);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            _abs.BlockWritesFor("corr:Patient");

            // Batch 1 claims the key and parks inside the write.
            await writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);
            await _abs.WaitUntilBlocked().WaitAsync(Timeout);

            // Batch 2 is handed off to the holder.
            await writer.EnqueueAsync("corr:Patient", Resources("Patient/2"), ResourceType.Patient);

            // The cancel makes batch 2 stale, but it stays attached.
            writer.Cancel(["corr:Patient"]);

            // Batch 3 is new work, enqueued after the cancel, and must reach storage.
            await writer.EnqueueAsync("corr:Patient", Resources("Patient/3"), ResourceType.Patient);

            _abs.ReleaseBlockedWrite();
            await writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout);

            var written = _abs.Writes.Where(write => write.CacheKey == "corr:Patient").ToList();

            // Batch 1 and batch 3. Batch 2 is correctly discarded by the cancel; batch 3 is not.
            Assert.Equal(2, written.Count);
        }
        finally
        {
            _abs.ReleaseBlockedWrite();
            await writer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task CancelledHandOff_IsAccountedForRatherThanStranded()
    {
        // A cancel can land while an item sits handed off. Nobody but the holder can account for it
        // at that point, and losing its decrement would leave the key permanently undrainable.
        var writer = CreateWriter(settings => settings.MaxConcurrency = 2);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            _abs.BlockWritesFor("corr:Patient");

            await writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);
            await _abs.WaitUntilBlocked().WaitAsync(Timeout);
            await writer.EnqueueAsync("corr:Patient", Resources("Patient/2"), ResourceType.Patient);

            writer.Cancel(["corr:Patient"]);
            _abs.ReleaseBlockedWrite();

            await writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout);

            // The cancelled hand-off was discarded, not written.
            Assert.Single(_abs.Writes.Where(write => write.CacheKey == "corr:Patient"));
        }
        finally
        {
            _abs.ReleaseBlockedWrite();
            await writer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task HandOffCapReached_FallsBackToWaiting_AndStillWritesEverything()
    {
        // The cap is what keeps the memory bound: a dequeued batch no longer holds a channel slot,
        // so merging without limit would grow outside what QueueCapacity bounds. At the ceiling a
        // worker waits for the key, as every worker used to.
        var writer = CreateWriter(settings =>
        {
            settings.MaxConcurrency = 3;
            settings.MaxCoalescedResources = 1;
        });

        await writer.StartAsync(CancellationToken.None);
        try
        {
            _abs.BlockWritesFor("corr:Patient");

            await writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);
            await _abs.WaitUntilBlocked().WaitAsync(Timeout);

            await writer.EnqueueAsync("corr:Patient", Resources("Patient/2"), ResourceType.Patient);
            await writer.EnqueueAsync("corr:Patient", Resources("Patient/3"), ResourceType.Patient);

            _abs.ReleaseBlockedWrite();
            await writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout);

            var writes = _abs.Writes.Where(write => write.CacheKey == "corr:Patient").ToList();

            // Exactly three. Two would mean the second and third merged despite the cap, and
            // "more than one" cannot tell those apart -- it is true either way.
            Assert.Equal(3, writes.Count);
            Assert.Equal(3, writes.Sum(write => write.Count));
            Assert.Equal(1, _abs.MaxConcurrentWritesFor("corr:Patient"));
        }
        finally
        {
            _abs.ReleaseBlockedWrite();
            await writer.StopAsync(CancellationToken.None);
        }
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

        // Blocked first, so the Patient batch is provably claimed and writing before the Encounter
        // batch is enqueued. Without that the two can merge into one write, which would burn all
        // three failures on the merged batch and leave nothing for the second to prove.
        _abs.BlockWritesFor("corr:Patient");

        await _writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);
        await _abs.WaitUntilBlocked().WaitAsync(Timeout);

        await _writer.EnqueueAsync("corr:Patient", Resources("Encounter/1"), ResourceType.Encounter);

        _abs.ReleaseBlockedWrite();

        // Both are in the same cycle and the caller waits once, after both. The Encounter batch is
        // durable and the Patient batch is not, so the key is not durable.
        await Assert.ThrowsAsync<ResourceCacheDurabilityException>(
            () => _writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout));
    }

    [Fact]
    public async Task ExhaustedWrite_SameResourcesRewritten_KeyRecovers()
    {
        // The caller fails its work and the message is redelivered, which rewrites the same
        // resources. The failure is no longer consumed when reported, so it is the rewrite landing
        // that clears it -- otherwise the message could never recover from a transient outage.
        _abs.FailKey("corr:Patient");
        await _writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);

        await Assert.ThrowsAsync<ResourceCacheDurabilityException>(
            () => _writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout));

        _abs.ClearFailures();
        await _writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);

        await _writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout);
    }

    [Fact]
    public async Task Cancel_WriteAlreadyExecuting_IsUndoneInDurableStorage()
    {
        // The case neither generation check covers: the write is already inside durable storage when
        // Cancel runs, so it has passed both. Blob storage has no expiry and the purge that triggers
        // the cancel exists to remove clinical data, so the write must not be allowed to stand.
        _abs.BlockWritesFor("corr:Patient");
        await _writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);
        await _abs.WaitUntilBlocked().WaitAsync(Timeout);

        _writer.Cancel(["corr:Patient"]);

        _abs.ReleaseBlockedWrite();
        await _writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout);

        // The write landed -- it was already in flight -- and was then undone.
        Assert.Single(_abs.Writes.Where(write => write.CacheKey == "corr:Patient"));
        Assert.Contains("corr:Patient", _abs.Deletes);
    }

    [Fact]
    public async Task SuccessfulWrite_RecordsTheDurableCountAgainstTheCacheEntry()
    {
        await _writer.EnqueueAsync("corr:Patient", Resources("Patient/1", "Patient/2"), ResourceType.Patient);
        await _writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout);

        // Recorded from durable storage once the write landed, because only a landed write gives a
        // count a reader can rely on.
        Assert.Equal(2, _redis.DurableCountsSet["corr:Patient"]);
    }

    [Fact]
    public async Task FailedWrite_RecordsNoDurableCount()
    {
        _abs.FailKey("corr:Patient");
        await _writer.EnqueueAsync("corr:Patient", Resources("Patient/1"), ResourceType.Patient);

        await Assert.ThrowsAsync<ResourceCacheDurabilityException>(
            () => _writer.WaitForDurableAsync(["corr:Patient"]).WaitAsync(Timeout));

        // Recording one here would claim durable storage holds resources it does not.
        Assert.False(_redis.DurableCountsSet.ContainsKey("corr:Patient"));
    }

    [Fact]
    public async Task WaitingOnOneKey_DoesNotConsumeAnotherKeysFailure()
    {
        // Two sibling acquisition logs write different keys of one correlation. A waiter on one key
        // is not told about the other's failure, and does not take it away from the log that owns it.
        _abs.FailKey("corr:Condition");

        await _writer.EnqueueAsync("corr:Observation", Resources("Observation/1"), ResourceType.Observation);
        await _writer.EnqueueAsync("corr:Condition", Resources("Condition/1"), ResourceType.Condition);

        // The sibling waits on its own key only and is unaffected.
        await _writer.WaitForDurableAsync(["corr:Observation"]).WaitAsync(Timeout);

        // The owner still gets told, which is the whole point.
        await Assert.ThrowsAsync<ResourceCacheDurabilityException>(
            () => _writer.WaitForDurableAsync(["corr:Condition"]).WaitAsync(Timeout));
    }

    [Fact]
    public async Task WaitForCorrelationAsync_KeyFailed_DoesNotConsumeTheOwnersFailure()
    {
        // A correlation-wide waiter used to consume every key's failure, after which the owner was
        // told its key was durable. Reporting no longer consumes, so both are told.
        _abs.FailKey("corr:Condition");

        await _writer.EnqueueAsync("corr:Observation", Resources("Observation/1"), ResourceType.Observation);
        await _writer.EnqueueAsync("corr:Condition", Resources("Condition/1"), ResourceType.Condition);

        await Assert.ThrowsAsync<ResourceCacheDurabilityException>(
            () => _writer.WaitForCorrelationAsync("corr").WaitAsync(Timeout));

        await Assert.ThrowsAsync<ResourceCacheDurabilityException>(
            () => _writer.WaitForDurableAsync(["corr:Condition"], ["Condition/1"]).WaitAsync(Timeout));
    }

    [Fact]
    public async Task WaitForDurableAsync_TwoWaitersOnAFailedMergedBatch_BothAreTold()
    {
        // H2: two logs on one pod write the same key and both wait on it. Their batches merge in a
        // hand-off and the merged write fails. Whichever waiter took the lock first used to consume
        // the failure, and the other -- possibly the log that owned it -- was told the key was durable.
        _abs.FailKey("corr:Observation");
        _abs.BlockWritesFor("corr:Observation");

        await _writer.EnqueueAsync("corr:Observation", Resources("Observation/a1"), ResourceType.Observation);
        await _abs.WaitUntilBlocked().WaitAsync(Timeout);

        // Log B's write hands off, and log A's next write merges into it. Two counted items become
        // one when they merge, so the queue depth says when that has happened.
        await _writer.EnqueueAsync("corr:Observation", Resources("Observation/b1"), ResourceType.Observation);
        await _writer.EnqueueAsync("corr:Observation", Resources("Observation/a2"), ResourceType.Observation);
        await WaitUntilAsync(() => _writer.QueueDepth == 2);

        var logA = _writer.WaitForDurableAsync(["corr:Observation"], ["Observation/a1", "Observation/a2"]);
        var logB = _writer.WaitForDurableAsync(["corr:Observation"], ["Observation/b1"]);
        var keyWaiterOne = _writer.WaitForDurableAsync(["corr:Observation"]);
        var keyWaiterTwo = _writer.WaitForDurableAsync(["corr:Observation"]);

        _abs.ReleaseBlockedWrite();

        await Assert.ThrowsAsync<ResourceCacheDurabilityException>(() => logA.WaitAsync(Timeout));
        await Assert.ThrowsAsync<ResourceCacheDurabilityException>(() => logB.WaitAsync(Timeout));
        await Assert.ThrowsAsync<ResourceCacheDurabilityException>(() => keyWaiterOne.WaitAsync(Timeout));
        await Assert.ThrowsAsync<ResourceCacheDurabilityException>(() => keyWaiterTwo.WaitAsync(Timeout));
    }

    [Fact]
    public async Task WaitForDurableAsync_ReferenceScoped_SiblingWhoseResourcesLanded_IsNotFailed()
    {
        // Same key, separate batches: log A's exhausts its retries, log B's lands. B is not failed
        // for A's write, so B is not sent round to re-acquire data that is already durable.
        _abs.FailKeyTimes("corr:Observation", 3);
        _abs.BlockWritesFor("corr:Observation");

        await _writer.EnqueueAsync("corr:Observation", Resources("Observation/a1"), ResourceType.Observation);
        await _abs.WaitUntilBlocked().WaitAsync(Timeout);
        await _writer.EnqueueAsync("corr:Observation", Resources("Observation/b1"), ResourceType.Observation);

        _abs.ReleaseBlockedWrite();

        await _writer.WaitForDurableAsync(["corr:Observation"], ["Observation/b1"]).WaitAsync(Timeout);
    }

    [Fact]
    public async Task WaitForDurableAsync_ReferenceScoped_OwnerWaitsAfterSibling_IsStillFailed()
    {
        // The exact H2 interleaving: the sibling reaches the barrier first. However it waits -- on its
        // own resources or on the whole key -- the owner of the failed write is still told afterwards.
        _abs.FailKeyTimes("corr:Observation", 3);
        _abs.BlockWritesFor("corr:Observation");

        await _writer.EnqueueAsync("corr:Observation", Resources("Observation/a1"), ResourceType.Observation);
        await _abs.WaitUntilBlocked().WaitAsync(Timeout);
        await _writer.EnqueueAsync("corr:Observation", Resources("Observation/b1"), ResourceType.Observation);

        _abs.ReleaseBlockedWrite();

        await _writer.WaitForDurableAsync(["corr:Observation"], ["Observation/b1"]).WaitAsync(Timeout);
        await Assert.ThrowsAsync<ResourceCacheDurabilityException>(
            () => _writer.WaitForDurableAsync(["corr:Observation"]).WaitAsync(Timeout));

        var thrown = await Assert.ThrowsAsync<ResourceCacheDurabilityException>(
            () => _writer.WaitForDurableAsync(["corr:Observation"], ["Observation/a1"]).WaitAsync(Timeout));
        Assert.Contains("Observation/a1", thrown.Message);
    }

    [Fact]
    public async Task WaitForDurableAsync_FailedReferencesRewritten_Succeeds()
    {
        // The redelivery of the failed log writes the same resources again. Once they land, neither
        // the owner nor a key-wide waiter is failed, and the key's state is let go.
        _abs.FailKey("corr:Observation");
        await _writer.EnqueueAsync("corr:Observation", Resources("Observation/a1", "Observation/a2"), ResourceType.Observation);
        await Assert.ThrowsAsync<ResourceCacheDurabilityException>(
            () => _writer.WaitForDurableAsync(["corr:Observation"], ["Observation/a1"]).WaitAsync(Timeout));

        _abs.ClearFailures();

        // Rewriting only part of it leaves the rest owed.
        await _writer.EnqueueAsync("corr:Observation", Resources("Observation/a1"), ResourceType.Observation);
        await Assert.ThrowsAsync<ResourceCacheDurabilityException>(
            () => _writer.WaitForDurableAsync(["corr:Observation"]).WaitAsync(Timeout));

        await _writer.EnqueueAsync("corr:Observation", Resources("Observation/a2"), ResourceType.Observation);
        await _writer.WaitForDurableAsync(["corr:Observation"], ["Observation/a1", "Observation/a2"]).WaitAsync(Timeout);
        await _writer.WaitForDurableAsync(["corr:Observation"]).WaitAsync(Timeout);

        Assert.Equal(0, _writer.TrackedKeyCount);
    }

    [Fact]
    public async Task WaitForDurableAsync_UnrelatedReferencesAfterFailure_AreNotFailed()
    {
        // A failure nobody has made good yet must not be handed to the next, unrelated log that
        // happens to write the same key.
        _abs.FailKey("corr:Observation");
        await _writer.EnqueueAsync("corr:Observation", Resources("Observation/a1"), ResourceType.Observation);
        await Assert.ThrowsAsync<ResourceCacheDurabilityException>(
            () => _writer.WaitForDurableAsync(["corr:Observation"], ["Observation/a1"]).WaitAsync(Timeout));

        _abs.ClearFailures();
        await _writer.EnqueueAsync("corr:Observation", Resources("Observation/c1"), ResourceType.Observation);

        await _writer.WaitForDurableAsync(["corr:Observation"], ["Observation/c1"]).WaitAsync(Timeout);
    }

    [Fact]
    public async Task WaitForDurableAsync_FailureWithinRetention_IsStillReported()
    {
        // The owning log only waits at the end of its execution, which can be hours after the write.
        _abs.FailKey("corr:Observation");
        await _writer.EnqueueAsync("corr:Observation", Resources("Observation/a1"), ResourceType.Observation);
        await WaitUntilAsync(() => _writer.QueueDepth == 0);

        _time.Advance(TimeSpan.FromHours(5));

        await Assert.ThrowsAsync<ResourceCacheDurabilityException>(
            () => _writer.WaitForDurableAsync(["corr:Observation"], ["Observation/a1"]).WaitAsync(Timeout));
    }

    [Fact]
    public async Task WaitForDurableAsync_FailureOlderThanRetention_IsNoLongerReported()
    {
        // The redelivery that made this good can land on another pod, so this pod may never see the
        // rewrite that would clear it. Kept forever, it failed every later key-wide wait here for data
        // durable storage already held.
        _abs.FailKey("corr:Observation");
        await _writer.EnqueueAsync("corr:Observation", Resources("Observation/a1"), ResourceType.Observation);
        await Assert.ThrowsAsync<ResourceCacheDurabilityException>(
            () => _writer.WaitForDurableAsync(["corr:Observation"]).WaitAsync(Timeout));

        _time.Advance(TimeSpan.FromHours(7));

        await _writer.WaitForDurableAsync(["corr:Observation"]).WaitAsync(Timeout);
        await _writer.WaitForDurableAsync(["corr:Observation"], ["Observation/a1"]).WaitAsync(Timeout);
    }

    [Fact]
    public async Task Sweep_ExpiredFailureOnAKeyNobodyTouchesAgain_ReleasesTheKey()
    {
        // Nothing writes or waits on this key again -- the retry went to another pod -- so only the
        // sweep can let it go. Without it the key was tracked for the life of the process.
        _abs.FailKey("corr:Observation");
        await _writer.EnqueueAsync("corr:Observation", Resources("Observation/a1"), ResourceType.Observation);
        await WaitUntilAsync(() => _writer.QueueDepth == 0);
        Assert.Equal(1, _writer.TrackedKeyCount);

        _time.Advance(TimeSpan.FromHours(7));

        await WaitUntilAsync(() => _writer.TrackedKeyCount == 0);
    }

    [Fact]
    public async Task Sweep_FailureWithinRetention_KeepsTheKey()
    {
        _abs.FailKey("corr:Observation");
        await _writer.EnqueueAsync("corr:Observation", Resources("Observation/a1"), ResourceType.Observation);
        await WaitUntilAsync(() => _writer.QueueDepth == 0);

        // Several sweeps run, none of them past the retention.
        for (var i = 0; i < 6; i++)
        {
            _time.Advance(TimeSpan.FromMinutes(30));
        }

        await Task.Delay(100);
        Assert.Equal(1, _writer.TrackedKeyCount);
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
            _redis,
            Options.Create(settings),
            Mock.Of<IResourceCacheMetrics>(),
            _time,
            Mock.Of<ILogger<BackgroundAbsCacheWriter>>());
    }

    /// <summary>
    /// Builds each resource as the type its reference names. Failures are tracked per reference, so
    /// "Encounter/1" built as a Patient would collide with "Patient/1".
    /// </summary>
    private static List<DomainResource> Resources(params string[] references)
    {
        return references
            .Select(reference =>
            {
                var parts = reference.Split('/');
                var resource = (DomainResource)Activator.CreateInstance(ModelInfo.GetTypeForFhirType(parts[0])!)!;
                resource.Id = parts[1];
                return resource;
            })
            .ToList();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met in time.");
            }

            await Task.Delay(10);
        }
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
        private bool _released;

        private TaskCompletionSource? _holdRelease;
        private TaskCompletionSource? _concurrencyReached;
        private int _holdTarget;
        private int _holdArrived;

        private readonly ConcurrentQueue<string> _deletes = new();

        public IReadOnlyList<(string CacheKey, int Count)> Writes => _writes.ToList();

        public IReadOnlyList<string> Deletes => _deletes.ToList();

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

            // Idempotent. A test that releases in its body is also released by DisposeAsync, and a
            // third Release would overflow the semaphore and surface as a SemaphoreFullException
            // from cleanup -- masking whatever the test was actually asserting.
            lock (_gate)
            {
                if (_released)
                {
                    return;
                }

                _released = true;
            }

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

        public async Task AppendResourcesAsync(
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

        public Task DeleteAsync(List<string> cacheKeys, CancellationToken cancellationToken = default)
        {
            foreach (var cacheKey in cacheKeys ?? [])
            {
                _deletes.Enqueue(cacheKey);
            }

            return Task.CompletedTask;
        }

        public Task<bool> HasResourcesAsync(string cacheKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public ConcurrentDictionary<string, int> DurableCountsSet { get; } = new();

        public Task<int> GetResourceCountAsync(string cacheKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(_writes.Where(write => write.CacheKey == cacheKey).Sum(write => write.Count));

        public Task<int?> GetDurableResourceCountAsync(string cacheKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(DurableCountsSet.TryGetValue(cacheKey, out var count) ? count : (int?)null);

        public Task<bool> IsEntryCompleteAsync(string cacheKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task SetDurableResourceCountAsync(string cacheKey, int count, CancellationToken cancellationToken = default)
        {
            DurableCountsSet[cacheKey] = count;
            return Task.CompletedTask;
        }

        public ResourceType GetResourceTypeByCacheKey(string cacheKey) => ResourceType.Patient;

        public Task ReplaceResourcesAsync(
            string cacheKey,
            List<DomainResource> resources,
            ResourceType resourceType,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task WaitForDurableAsync(IEnumerable<string> cacheKeys, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WaitForDurableAsync(IEnumerable<string> cacheKeys,
                                        IReadOnlyCollection<string> references,
                                        CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WaitForDurableAsync(string correlationId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
