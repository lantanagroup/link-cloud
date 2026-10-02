using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Shared.Application.Services.ResourceCache
{
    /// <inheritdoc cref="IBackgroundAbsCacheWriter"/>
    /// <remarks>
    /// Writes to one cache key are serialized, because the blob write reads the key's id list before
    /// appending to it and two concurrent writers would both miss the other's resources. Writes to
    /// different keys run concurrently, bounded by
    /// <see cref="ResourceCacheAbsWriterSettings.MaxConcurrency"/>.
    /// <para>
    /// A worker that finds a key already being written does not wait for it. It hands its batch to
    /// the worker holding the key, which writes it before giving the key up, and goes back for other
    /// keys. Without that, one correlation -- which writes its key once per acquired resource type,
    /// commonly more times than there are workers -- parks the whole pool on a single key.
    /// </para>
    /// </remarks>
    public class BackgroundAbsCacheWriter : BackgroundService, IBackgroundAbsCacheWriter
    {
        private readonly IResourceCache _absCache;
        private readonly IResourceCache _redisCache;
        private readonly ResourceCacheAbsWriterSettings _settings;
        private readonly IResourceCacheMetrics _metrics;
        private readonly ILogger<BackgroundAbsCacheWriter> _logger;
        private readonly Channel<PendingWrite> _queue;
        private readonly ConcurrentDictionary<string, KeyState> _keys = new();
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// How long a failed write is reported for. Longer than an acquisition log can run before
        /// stall recovery resets it (240 minutes by default), because the log that owns a failure only
        /// waits at the end of its execution. Past that the owner has been re-run regardless, and a
        /// failure kept any longer only fails work it does not belong to -- or, once its redelivery
        /// landed on another pod, work over data durable storage already holds.
        /// </summary>
        private static readonly TimeSpan FailedReferenceRetention = TimeSpan.FromHours(6);

        /// <summary>
        /// How often expired failures are swept, so a key nobody writes or waits on again is released.
        /// </summary>
        private static readonly TimeSpan FailureSweepInterval = TimeSpan.FromMinutes(10);

        public BackgroundAbsCacheWriter(
            [FromKeyedServices(ResourceCacheType.ABS)] IResourceCache absCache,
            [FromKeyedServices(ResourceCacheType.Redis)] IResourceCache redisCache,
            IOptions<ResourceCacheSettings> settings,
            IResourceCacheMetrics metrics,
            TimeProvider timeProvider,
            ILogger<BackgroundAbsCacheWriter> logger)
        {
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            _absCache = absCache ?? throw new ArgumentNullException(nameof(absCache));
            _redisCache = redisCache ?? throw new ArgumentNullException(nameof(redisCache));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = settings?.Value?.AbsWriter ?? throw new ArgumentNullException(nameof(settings));

            if (_settings.QueueCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(settings),
                    "ResourceCache:AbsWriter:QueueCapacity must be greater than zero.");
            }

            if (_settings.MaxConcurrency <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(settings),
                    "ResourceCache:AbsWriter:MaxConcurrency must be greater than zero.");
            }

            if (_settings.MaxCoalescedResources <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(settings),
                    "ResourceCache:AbsWriter:MaxCoalescedResources must be greater than zero.");
            }

            _metrics.TrackQueueDepth(() => QueueDepth);

            _queue = Channel.CreateBounded<PendingWrite>(new BoundedChannelOptions(_settings.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            });
        }

        /// <summary>
        /// The number of writes queued but not yet persisted. Exposed for health and metrics.
        /// </summary>
        public int QueueDepth => _keys.Values.Sum(state => state.Outstanding);

        /// <summary>
        /// How many cache keys the writer is currently tracking. Non-zero only while writes are
        /// outstanding or a key's last write failed.
        /// </summary>
        public int TrackedKeyCount => _keys.Count;

        /// <inheritdoc/>
        public async Task EnqueueAsync(
            string cacheKey,
            List<DomainResource> resources,
            ResourceType resourceType,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(cacheKey);

            if (resources == null || resources.Count == 0)
            {
                return;
            }

            KeyState state;
            int generation;

            // A state that drained to nothing is removed, so it can be retired between the GetOrAdd
            // and the lock. Retry against whatever is there now rather than counting into a corpse.
            while (true)
            {
                state = _keys.GetOrAdd(cacheKey, _ => new KeyState());
                lock (state.Gate)
                {
                    if (state.Retired)
                    {
                        continue;
                    }

                    state.Outstanding++;
                    generation = state.Generation;
                    break;
                }
            }

            try
            {
                await _queue.Writer.WriteAsync(
                    new PendingWrite(cacheKey, state, resources, resourceType, generation, Stopwatch.GetTimestamp()),
                    cancellationToken);
            }
            catch (Exception)
            {
                // The write never made it onto the queue, so nothing will ever decrement for it.
                CompleteOne(cacheKey, state, written: null, failure: null);
                throw;
            }
        }

        /// <inheritdoc/>
        public Task WaitForDurableAsync(
            IEnumerable<string> cacheKeys,
            CancellationToken cancellationToken = default)
        {
            return WaitForDurableCoreAsync(cacheKeys, references: null, cancellationToken);
        }

        /// <inheritdoc/>
        public Task WaitForDurableAsync(
            IEnumerable<string> cacheKeys,
            IReadOnlyCollection<string> references,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(references);

            return WaitForDurableCoreAsync(cacheKeys, references, cancellationToken);
        }

        private async Task WaitForDurableCoreAsync(
            IEnumerable<string> cacheKeys,
            IReadOnlyCollection<string>? references,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(cacheKeys);

            foreach (var cacheKey in cacheKeys.Where(key => !string.IsNullOrEmpty(key)).Distinct())
            {
                if (!_keys.TryGetValue(cacheKey, out var state))
                {
                    continue;
                }

                Task completion;
                lock (state.Gate)
                {
                    if (state.Outstanding == 0)
                    {
                        ThrowIfFailed(cacheKey, state, references, _timeProvider.GetUtcNow());
                        continue;
                    }

                    completion = state.Completion.Task;
                }

                await completion.WaitAsync(cancellationToken);

                lock (state.Gate)
                {
                    ThrowIfFailed(cacheKey, state, references, _timeProvider.GetUtcNow());
                }
            }
        }

        /// <inheritdoc/>
        public Task WaitForCorrelationAsync(string correlationId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(correlationId))
            {
                return Task.CompletedTask;
            }

            var prefix = correlationId + ":";
            var keys = _keys.Keys
                .Where(key => key == correlationId || key.StartsWith(prefix, StringComparison.Ordinal))
                .ToList();

            return WaitForDurableCoreAsync(keys, references: null, cancellationToken);
        }

        /// <inheritdoc/>
        public void Cancel(IEnumerable<string> cacheKeys)
        {
            if (cacheKeys == null)
            {
                return;
            }

            foreach (var cacheKey in cacheKeys.Where(key => !string.IsNullOrEmpty(key)))
            {
                if (!_keys.TryGetValue(cacheKey, out var state))
                {
                    continue;
                }

                lock (state.Gate)
                {
                    // Anything queued under the old generation is skipped when it is dequeued. The
                    // recorded failure goes too: the key is being removed, so it is no longer owed.
                    state.Generation++;
                    state.ClearFailure();
                    SignalIfDrained(cacheKey, state);
                }
            }
        }

        /// <inheritdoc/>
        public async Task CancelAndDrainAsync(
            IEnumerable<string> cacheKeys,
            CancellationToken cancellationToken = default)
        {
            if (cacheKeys == null)
            {
                return;
            }

            foreach (var cacheKey in cacheKeys.Where(key => !string.IsNullOrEmpty(key)).Distinct())
            {
                if (!_keys.TryGetValue(cacheKey, out var state))
                {
                    continue;
                }

                Task completion;

                lock (state.Gate)
                {
                    state.Generation++;
                    state.ClearFailure();
                    SignalIfDrained(cacheKey, state);

                    if (state.Outstanding == 0)
                    {
                        continue;
                    }

                    // Captured under the lock, because SignalIfDrained replaces the source once it
                    // completes and the next one belongs to a later batch.
                    completion = state.Completion.Task;
                }

                await completion.WaitAsync(cancellationToken);

                lock (state.Gate)
                {
                    // A write that failed while draining recorded a failure after the clear above.
                    // It describes contents the caller is about to replace outright, so reporting it
                    // to whoever waits on this key next would fail work over a copy that no longer
                    // exists.
                    state.ClearFailure();

                    if (state.Outstanding == 0 && !state.Retired)
                    {
                        // SignalIfDrained keeps a failed key alive so its waiter can still see the
                        // failure. Nothing is owed one now, so the state goes.
                        state.Retired = true;
                        _keys.TryRemove(new KeyValuePair<string, KeyState>(cacheKey, state));
                    }
                }
            }
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var workers = Enumerable
                .Range(0, _settings.MaxConcurrency)
                .Select(_ => Task.Run(() => ConsumeAsync(stoppingToken), CancellationToken.None))
                .Append(Task.Run(() => SweepExpiredFailuresAsync(stoppingToken), CancellationToken.None))
                .ToArray();

            await Task.WhenAll(workers);
        }

        /// <summary>
        /// Drops failures older than <see cref="FailedReferenceRetention"/>, and releases keys left with
        /// nothing outstanding and nothing owed.
        /// </summary>
        /// <remarks>
        /// Without it a key whose failed resources are never written again on this pod -- the retry
        /// landed elsewhere, or the work failed terminally -- stays tracked for the life of the process.
        /// </remarks>
        private async Task SweepExpiredFailuresAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(FailureSweepInterval, _timeProvider);

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    try
                    {
                        SweepExpiredFailures();
                    }
                    catch (Exception ex)
                    {
                        // Housekeeping. A fault here must not end the host's background service.
                        _logger.LogWarning(ex, "Resource cache blob writer could not sweep expired write failures.");
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down.
            }
        }

        private void SweepExpiredFailures()
        {
            var now = _timeProvider.GetUtcNow();

            foreach (var (cacheKey, state) in _keys)
            {
                lock (state.Gate)
                {
                    if (state.Retired)
                    {
                        continue;
                    }

                    PruneExpiredFailures(state, now);

                    if (state.Outstanding == 0 && state.FailedReferences.Count == 0)
                    {
                        state.Retired = true;
                        _keys.TryRemove(new KeyValuePair<string, KeyState>(cacheKey, state));
                    }
                }
            }
        }

        /// <remarks>Callers hold <see cref="KeyState.Gate"/>.</remarks>
        private static void PruneExpiredFailures(KeyState state, DateTimeOffset now)
        {
            if (state.FailedReferences.Count == 0)
            {
                return;
            }

            var expired = state.FailedReferences
                .Where(failed => now - failed.Value >= FailedReferenceRetention)
                .Select(failed => failed.Key)
                .ToList();

            foreach (var reference in expired)
            {
                state.FailedReferences.Remove(reference);
            }

            if (state.FailedReferences.Count == 0)
            {
                state.LastFailure = null;
            }
        }

        /// <summary>
        /// Stops accepting writes and gives the queue up to
        /// <see cref="ResourceCacheAbsWriterSettings.DrainTimeoutSeconds"/> to finish.
        /// </summary>
        /// <remarks>
        /// The drain deliberately does not use the stopping token: passing it would cancel the drain
        /// the moment shutdown began, which is the opposite of what is wanted.
        /// </remarks>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _queue.Writer.TryComplete();

            using var drainTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            drainTimeout.CancelAfter(TimeSpan.FromSeconds(_settings.DrainTimeoutSeconds));

            try
            {
                await _queue.Reader.Completion.WaitAsync(drainTimeout.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Resource cache blob writer shut down with {PendingWriteCount} write(s) still queued after " +
                    "{DrainTimeoutSeconds}s. Their cache keys were never advertised, so the work that produced " +
                    "them will be recovered and retried.",
                    QueueDepth,
                    _settings.DrainTimeoutSeconds);
            }

            try
            {
                // Bounded deliberately. The base implementation waits for the worker loops, and a
                // blob call that never returns would otherwise hold shutdown open indefinitely.
                await base.StopAsync(drainTimeout.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Resource cache blob writer had {PendingWriteCount} write(s) still in progress at shutdown " +
                    "and stopped without waiting for them.",
                    QueueDepth);
            }
        }

        // -------------------------------------------------------------------------

        private static double Elapsed(long startTimestamp) =>
            Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

        private async Task ConsumeAsync(CancellationToken stoppingToken)
        {
            try
            {
                await foreach (var pending in _queue.Reader.ReadAllAsync(CancellationToken.None))
                {
                    await ProcessAsync(pending, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                // A worker that dies takes its share of the throughput with it, and nothing restarts
                // it, so this is worth surfacing rather than swallowing.
                _logger.LogError(ex, "Resource cache blob writer worker stopped unexpectedly.");
            }
        }

        private async Task ProcessAsync(PendingWrite pending, CancellationToken stoppingToken)
        {
            // The state travels with the item. Looking it up again could find a different instance,
            // because a key that drains to nothing is retired and a later write creates a fresh one.
            var state = pending.State;
            var current = pending;

            // Claim the key, or give the work to whoever already holds it. Claiming and checking the
            // generation happen in one critical section, so a cancel cannot land between them, which
            // is what the second check used to cover.
            while (true)
            {
                Task vacancy;

                lock (state.Gate)
                {
                    if (current.Generation != state.Generation)
                    {
                        state.Outstanding--;
                        SignalIfDrained(current.CacheKey, state);
                        return;
                    }

                    if (!state.Writing)
                    {
                        state.Writing = true;
                        break;
                    }

                    // Busy. Hand the work over and go find another key. Waiting here is what lets a
                    // single correlation -- which writes this key once per acquired resource type,
                    // commonly more times than there are workers -- park the entire pool on one key.

                    // A cancel bumps the generation but leaves the hand-off attached until the
                    // holder collects it. Merging into one of those loses this write: the holder sees
                    // a stale batch and discards all of it, while the waiter is still told the key is
                    // durable. Drop it here instead, taking the count it was still owed with it.
                    if (state.HandedOff is not null && state.HandedOff.Generation != current.Generation)
                    {
                        state.HandedOff = null;
                        state.Outstanding--;
                    }

                    if (state.HandedOff is null)
                    {
                        // Copied, because a later hand-off appends to this list and the original
                        // belongs to whoever enqueued it.
                        state.HandedOff = current with { Resources = [.. current.Resources] };
                        return;
                    }

                    if (state.HandedOff.Resources.Count + current.Resources.Count <= _settings.MaxCoalescedResources)
                    {
                        state.HandedOff.Resources.AddRange(current.Resources);

                        // Two counted items became one, so one count goes with it.
                        state.Outstanding--;
                        SignalIfDrained(current.CacheKey, state);
                        return;
                    }

                    // Merging further would grow one batch without bound, and a dequeued item no
                    // longer occupies a channel slot, so the queue capacity would not cover it.
                    // Wait for the key instead: the old behaviour, now reached only in the case the
                    // bound exists for.
                    vacancy = (state.Vacancy ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
                }

                await vacancy;
            }

            try
            {
                PendingWrite? item = current;

                while (item is not null)
                {
                    await WriteOneAsync(item, state, stoppingToken);
                    item = TakeHandedOffOrRelease(state);
                }
            }
            catch (Exception ex)
            {
                // WriteOneAsync reports every failure itself, so this should be unreachable. Handled
                // regardless: a hand-off left attached is counted in Outstanding with nobody to run
                // it, and every waiter on this key -- WaitForDurableAsync and CancelAndDrainAsync
                // alike -- would then block forever.
                ReleaseAndFailOwnedWork(state, ex);
                throw;
            }
        }

        /// <summary>
        /// Takes the next item handed to this writer, or gives up the key.
        /// </summary>
        /// <remarks>
        /// The check and the release are one critical section with the hand-off in
        /// <see cref="ProcessAsync"/>, so a hand-off can never arrive into a key that is being given
        /// up. Either the donor sees <see cref="KeyState.Writing"/> and attaches, and is collected
        /// here, or it sees the key free and claims it.
        /// </remarks>
        private PendingWrite? TakeHandedOffOrRelease(KeyState state)
        {
            lock (state.Gate)
            {
                while (true)
                {
                    var next = state.HandedOff;
                    state.HandedOff = null;

                    if (next is null)
                    {
                        state.Writing = false;
                        state.Vacancy?.TrySetResult();
                        state.Vacancy = null;
                        return null;
                    }

                    if (next.Generation == state.Generation)
                    {
                        return next;
                    }

                    // Cancelled while it sat attached. The donor has already returned, so this is the
                    // only place left that can account for it.
                    state.Outstanding--;
                    SignalIfDrained(next.CacheKey, state);
                }
            }
        }

        /// <summary>
        /// Gives up the key after an unexpected fault, failing any work still attached to it.
        /// </summary>
        private void ReleaseAndFailOwnedWork(KeyState state, Exception failure)
        {
            PendingWrite? stranded;

            lock (state.Gate)
            {
                stranded = state.HandedOff;
                state.HandedOff = null;
                state.Writing = false;
                state.Vacancy?.TrySetResult();
                state.Vacancy = null;
            }

            if (stranded is not null)
            {
                // Reported as failed rather than silently dropped: losing its decrement would leave
                // the key permanently undrainable.
                CompleteOne(stranded.CacheKey, state, stranded, failure);
            }
        }

        private async Task WriteOneAsync(PendingWrite pending, KeyState state, CancellationToken stoppingToken)
        {
            Exception? failure = null;
            var writeStart = Stopwatch.GetTimestamp();

            try
            {
                // Measured from enqueue to the start of the write, so a backlog is distinguishable
                // from storage having slowed down.
                _metrics.RecordQueueWait(Stopwatch.GetElapsedTime(pending.QueuedAtTimestamp).TotalMilliseconds);
                writeStart = Stopwatch.GetTimestamp();

                await WriteWithRetryAsync(pending, stoppingToken);
                _metrics.RecordWrite(ResourceCacheStores.Blob, ResourceCacheOutcomes.Ok, Elapsed(writeStart));

                // Still holding the write lock, so nothing else for this key can interleave.
                await CompensateIfCancelledAsync(pending, state);
                await PublishDurableCountAsync(pending.CacheKey);
            }
            catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
            {
                // Shutdown cut the backoff short, so the write never got its remaining attempts.
                // Still a failure -- the key is not durable and whoever waits on it has to be told --
                // but not an exhausted one, which would report a storage problem on every deployment.
                failure = ex;
                _metrics.RecordWrite(ResourceCacheStores.Blob, ResourceCacheOutcomes.Failed, Elapsed(writeStart));
                _metrics.IncrementWriteRetry(ResourceCacheOutcomes.Interrupted);
                _logger.LogWarning(
                    "Shutdown interrupted the retry backoff for resource cache key {CacheKey} before its "
                    + "remaining attempts could run. Anything waiting on this key will be told it is not durable.",
                    pending.CacheKey.SanitizeForLog());
            }
            catch (Exception ex)
            {
                failure = ex;
                _metrics.RecordWrite(ResourceCacheStores.Blob, ResourceCacheOutcomes.Failed, Elapsed(writeStart));
                _metrics.IncrementWriteRetry(ResourceCacheOutcomes.Exhausted);
                _logger.LogError(
                    ex,
                    "Failed to persist resource cache key {CacheKey} to blob storage after {AttemptCount} attempt(s). " +
                    "Anything waiting on this key will be told it is not durable.",
                    pending.CacheKey.SanitizeForLog(),
                    _settings.MaxRetryAttempts);
            }
            finally
            {
                CompleteOne(pending.CacheKey, state, pending, failure);
            }
        }

        /// <summary>
        /// Records against the cache entry how many resources durable storage now holds for the key.
        /// </summary>
        /// <remarks>
        /// This is what lets a reader tell a cache entry recreated by a partial append from a whole one.
        /// It runs here rather than on the write path because only a landed durable write gives a count a
        /// reader can rely on, and because the cost belongs on this thread rather than the caller's.
        /// Best effort: a missing count is read as "unknown" and simply costs the detection, not
        /// correctness of the data itself.
        /// </remarks>
        private async Task PublishDurableCountAsync(string cacheKey)
        {
            try
            {
                var durableCount = await _absCache.GetResourceCountAsync(cacheKey, CancellationToken.None);

                if (durableCount > 0)
                {
                    await _redisCache.SetDurableResourceCountAsync(cacheKey, durableCount, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not record the durable resource count for {CacheKey}. A partial cache entry for "
                    + "this key would not be detected until the next durable write records one.",
                    cacheKey.SanitizeForLog());
            }
        }

        /// <summary>
        /// Undoes a durable write that finished after its key was cancelled.
        /// </summary>
        /// <remarks>
        /// A write already executing has passed both generation checks, so <see cref="Cancel"/> and the
        /// delete that follows it can complete underneath it. Blob storage has no expiry, so a write
        /// landing after that delete leaves resources behind permanently -- and the purge that triggered
        /// it exists to remove clinical data after a terminal failure or a pipeline abort. Fencing the
        /// delete would mean blocking it on this write; undoing the write afterwards reaches the same
        /// end state from either order without holding the delete up.
        /// </remarks>
        private async Task CompensateIfCancelledAsync(PendingWrite pending, KeyState state)
        {
            lock (state.Gate)
            {
                if (pending.Generation == state.Generation)
                {
                    return;
                }
            }

            try
            {
                // Deliberately not the host's token. This runs to remove data the system decided to
                // discard, so shutdown must not be the reason it is skipped.
                await _absCache.DeleteAsync([pending.CacheKey], CancellationToken.None);

                _logger.LogWarning(
                    "Removed resource cache key {CacheKey} from blob storage: its durable write finished "
                    + "after the key was cancelled, so the delete that followed the cancel could not have "
                    + "covered it.",
                    pending.CacheKey.SanitizeForLog());
            }
            catch (Exception ex)
            {
                // Nothing else will retry this. Surfaced loudly because the residue is clinical data
                // that a purge already decided to remove.
                _logger.LogError(
                    ex,
                    "Could not remove resource cache key {CacheKey} from blob storage after its durable "
                    + "write finished past a cancel. The key may still hold resources that were purged "
                    + "from the cache.",
                    pending.CacheKey.SanitizeForLog());
            }
        }

        private async Task WriteWithRetryAsync(PendingWrite pending, CancellationToken stoppingToken)
        {
            var delay = TimeSpan.FromMilliseconds(Math.Max(1, _settings.RetryBaseDelayMilliseconds));

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await _absCache.AppendResourcesAsync(
                        pending.CacheKey,
                        pending.Resources,
                        pending.ResourceType,
                        CancellationToken.None);
                    return;
                }
                catch (Exception ex) when (attempt < _settings.MaxRetryAttempts)
                {
                    _metrics.IncrementWriteRetry(ResourceCacheOutcomes.Retried);
                    _logger.LogDebug(
                        ex,
                        "Retrying blob write for resource cache key {CacheKey}, attempt {Attempt} of {MaxAttempts}.",
                        pending.CacheKey.SanitizeForLog(),
                        attempt,
                        _settings.MaxRetryAttempts);

                    // Shutdown stops the backoff, not the write: an in-flight attempt is allowed to
                    // finish so the drain has a chance to complete.
                    await Task.Delay(delay, stoppingToken);
                    delay += delay;
                }
            }
        }

        /// <param name="cacheKey">The key the write was for.</param>
        /// <param name="state">The key's state.</param>
        /// <param name="written">The write that ran, or null when it never reached the queue.</param>
        /// <param name="failure">Why the write failed, or null when it landed.</param>
        private void CompleteOne(string cacheKey, KeyState state, PendingWrite? written, Exception? failure)
        {
            lock (state.Gate)
            {
                state.Outstanding--;

                if (written != null)
                {
                    var now = _timeProvider.GetUtcNow();

                    // Recorded per resource rather than per key. Several logs write and wait on one
                    // key, and a hand-off can merge their resources into one batch, so only the
                    // resources say whose work failed. A landed batch clears exactly the resources it
                    // carried: a later batch of *different* resources landing says nothing about an
                    // earlier one that never did.
                    foreach (var reference in References(written))
                    {
                        if (failure != null)
                        {
                            state.FailedReferences[reference] = now;
                        }
                        else
                        {
                            state.FailedReferences.Remove(reference);
                        }
                    }

                    if (failure != null)
                    {
                        state.LastFailure = failure;
                    }
                }

                SignalIfDrained(cacheKey, state);
            }
        }

        private static IEnumerable<string> References(PendingWrite write) =>
            write.Resources.Select(resource => resource.TypeName + "/" + resource.Id);

        /// <remarks>
        /// Callers hold <see cref="KeyState.Gate"/>. A key that drained cleanly is retired from the
        /// dictionary, or it would grow by one entry per cache key for the life of the process. A key
        /// holding a failure is kept, because something still has to be told about it.
        /// </remarks>
        private void SignalIfDrained(string cacheKey, KeyState state)
        {
            if (state.Outstanding > 0)
            {
                return;
            }

            state.Completion.TrySetResult();
            state.Completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            if (state.FailedReferences.Count == 0)
            {
                state.Retired = true;

                // Remove this instance specifically. A plain TryRemove(key) would drop whichever
                // state is there now, which may be a fresh one another writer just added and is
                // already counting into -- and losing that makes its key look durable when it is not.
                _keys.TryRemove(new KeyValuePair<string, KeyState>(cacheKey, state));
            }
        }

        /// <remarks>
        /// Callers hold <see cref="KeyState.Gate"/>. Reporting a failure does not consume it. When it
        /// did, only the first of several waiters on one key was told -- possibly a sibling log whose
        /// own resources had landed, leaving the log that owned the failure to be told the key was
        /// durable. A failure now lasts until the resources it covers are written again, which a
        /// redelivery does before it reaches this check, so recovery still works.
        /// </remarks>
        /// <param name="cacheKey">The key being checked.</param>
        /// <param name="state">The key's state.</param>
        /// <param name="references">
        /// The resources the caller wrote, or null to be told about any failure on the key.
        /// </param>
        /// <param name="now">The current time, for dropping failures past their retention.</param>
        private static void ThrowIfFailed(string cacheKey,
                                          KeyState state,
                                          IReadOnlyCollection<string>? references,
                                          DateTimeOffset now)
        {
            PruneExpiredFailures(state, now);

            if (state.FailedReferences.Count == 0)
            {
                return;
            }

            var owed = references == null
                ? state.FailedReferences.Keys.ToList()
                : references.Where(state.FailedReferences.ContainsKey).ToList();

            if (owed.Count == 0)
            {
                return;
            }

            throw new ResourceCacheDurabilityException(
                $"Resource cache key '{cacheKey}' could not be written to durable storage: {owed.Count} "
                + $"resource(s) did not land, including '{owed[0]}'.",
                state.LastFailure);
        }

        private sealed record PendingWrite(
            string CacheKey,
            KeyState State,
            List<DomainResource> Resources,
            ResourceType ResourceType,
            int Generation,
            long QueuedAtTimestamp);

        private sealed class KeyState
        {
            public readonly object Gate = new();
            public int Outstanding;
            public int Generation;

            /// <summary>
            /// Resources whose durable write failed and has not been made good by a later one, with
            /// when each failed, so a failure is reported for a bounded time rather than forever.
            /// </summary>
            public readonly Dictionary<string, DateTimeOffset> FailedReferences = new(StringComparer.Ordinal);

            /// <summary>
            /// The most recent failure, kept as the cause to report.
            /// </summary>
            public Exception? LastFailure;

            /// <summary>
            /// Set when this instance has been taken out of the dictionary. A writer that reached it
            /// through a stale lookup must go round again rather than counting into it.
            /// </summary>
            public bool Retired;

            /// <summary>
            /// Whether a worker is inside this key's write section. Replaces a SemaphoreSlim:
            /// nothing waits on it in the normal path, so a flag under <see cref="Gate"/> makes
            /// releasing it under the same lock that guards the hand-off impossible to get wrong.
            /// </summary>
            public bool Writing;

            /// <summary>
            /// Work a worker could not start because this key was busy, for the holder to pick up
            /// before it gives the key up. Further hand-offs merge into it, so a key accumulates at
            /// most one follow-up write however many arrive.
            /// </summary>
            public PendingWrite? HandedOff;

            /// <summary>
            /// Signalled when the key is given up. Only used when a hand-off would exceed
            /// <see cref="ResourceCacheAbsWriterSettings.MaxCoalescedResources"/> and a worker has
            /// to wait, as every worker did before hand-off existed.
            /// </summary>
            public TaskCompletionSource? Vacancy;

            public TaskCompletionSource Completion { get; set; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// Forgets every recorded failure. Only for a key whose contents are being removed or replaced.
            /// </summary>
            public void ClearFailure()
            {
                FailedReferences.Clear();
                LastFailure = null;
            }
        }
    }
}
