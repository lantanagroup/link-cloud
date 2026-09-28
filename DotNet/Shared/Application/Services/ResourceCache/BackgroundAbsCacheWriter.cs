using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
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
    /// </remarks>
    public class BackgroundAbsCacheWriter : BackgroundService, IBackgroundAbsCacheWriter
    {
        private readonly IResourceCache _absCache;
        private readonly ResourceCacheAbsWriterSettings _settings;
        private readonly ILogger<BackgroundAbsCacheWriter> _logger;
        private readonly Channel<PendingWrite> _queue;
        private readonly ConcurrentDictionary<string, KeyState> _keys = new();

        public BackgroundAbsCacheWriter(
            [FromKeyedServices(ResourceCacheType.ABS)] IResourceCache absCache,
            IOptions<ResourceCacheSettings> settings,
            ILogger<BackgroundAbsCacheWriter> logger)
        {
            _absCache = absCache ?? throw new ArgumentNullException(nameof(absCache));
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
                    new PendingWrite(cacheKey, state, resources, resourceType, generation),
                    cancellationToken);
            }
            catch (Exception)
            {
                // The write never made it onto the queue, so nothing will ever decrement for it.
                CompleteOne(cacheKey, state, failure: null, clearsFailure: false);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task WaitForDurableAsync(
            IEnumerable<string> cacheKeys,
            CancellationToken cancellationToken = default)
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
                        ThrowIfFailed(cacheKey, state);
                        continue;
                    }

                    completion = state.Completion.Task;
                }

                await completion.WaitAsync(cancellationToken);

                lock (state.Gate)
                {
                    ThrowIfFailed(cacheKey, state);
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

            return WaitForDurableAsync(keys, cancellationToken);
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
                    state.Failure = null;
                    SignalIfDrained(cacheKey, state);
                }
            }
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var workers = Enumerable
                .Range(0, _settings.MaxConcurrency)
                .Select(_ => Task.Run(() => ConsumeAsync(stoppingToken), CancellationToken.None))
                .ToArray();

            await Task.WhenAll(workers);
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

            lock (state.Gate)
            {
                if (pending.Generation != state.Generation)
                {
                    state.Outstanding--;
                    SignalIfDrained(pending.CacheKey, state);
                    return;
                }
            }

            Exception? failure = null;

            await state.WriteLock.WaitAsync(CancellationToken.None);
            try
            {
                await WriteWithRetryAsync(pending, stoppingToken);
            }
            catch (Exception ex)
            {
                failure = ex;
                _logger.LogError(
                    ex,
                    "Failed to persist resource cache key {CacheKey} to blob storage after {AttemptCount} attempt(s). " +
                    "Anything waiting on this key will be told it is not durable.",
                    pending.CacheKey.SanitizeForLog(),
                    _settings.MaxRetryAttempts);
            }
            finally
            {
                state.WriteLock.Release();
                CompleteOne(pending.CacheKey, state, failure, clearsFailure: true);
            }
        }

        private async Task WriteWithRetryAsync(PendingWrite pending, CancellationToken stoppingToken)
        {
            var delay = TimeSpan.FromMilliseconds(Math.Max(1, _settings.RetryBaseDelayMilliseconds));

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await _absCache.UpdateCorrelationCacheAsync(
                        pending.CacheKey,
                        pending.Resources,
                        pending.ResourceType,
                        CancellationToken.None);
                    return;
                }
                catch (Exception ex) when (attempt < _settings.MaxRetryAttempts)
                {
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

        private void CompleteOne(string cacheKey, KeyState state, Exception? failure, bool clearsFailure)
        {
            lock (state.Gate)
            {
                state.Outstanding--;

                if (failure != null)
                {
                    state.Failure = failure;
                }
                else if (clearsFailure)
                {
                    // A later write landing clears an earlier failure: the key is durable again.
                    state.Failure = null;
                }

                SignalIfDrained(cacheKey, state);
            }
        }

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

            if (state.Failure == null)
            {
                state.Retired = true;

                // Remove this instance specifically. A plain TryRemove(key) would drop whichever
                // state is there now, which may be a fresh one another writer just added and is
                // already counting into -- and losing that makes its key look durable when it is not.
                _keys.TryRemove(new KeyValuePair<string, KeyState>(cacheKey, state));
            }
        }

        /// <remarks>Callers hold <see cref="KeyState.Gate"/>.</remarks>
        private static void ThrowIfFailed(string cacheKey, KeyState state)
        {
            if (state.Failure == null)
            {
                return;
            }

            throw new ResourceCacheDurabilityException(
                $"Resource cache key '{cacheKey}' could not be written to durable storage.",
                state.Failure);
        }

        private sealed record PendingWrite(
            string CacheKey,
            KeyState State,
            List<DomainResource> Resources,
            ResourceType ResourceType,
            int Generation);

        private sealed class KeyState
        {
            public readonly object Gate = new();
            public readonly SemaphoreSlim WriteLock = new(1, 1);
            public int Outstanding;
            public int Generation;
            public Exception? Failure;

            /// <summary>
            /// Set when this instance has been taken out of the dictionary. A writer that reached it
            /// through a stale lookup must go round again rather than counting into it.
            /// </summary>
            public bool Retired;

            public TaskCompletionSource Completion { get; set; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
