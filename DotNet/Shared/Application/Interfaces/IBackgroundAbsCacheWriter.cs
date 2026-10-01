using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Shared.Application.Interfaces
{
    /// <summary>
    /// Persists resource cache entries to blob storage on a background queue, so that callers pay
    /// only the cost of the in-memory cache write.
    /// </summary>
    /// <remarks>
    /// One instance per process. A queue is never visible to another pod, which is why
    /// <see cref="WaitForDurableAsync"/> can only speak for writes this process enqueued. See
    /// docs-dev/resource-cache.md.
    /// </remarks>
    public interface IBackgroundAbsCacheWriter
    {
        /// <summary>
        /// Queues <paramref name="resources"/> to be persisted under <paramref name="cacheKey"/>.
        /// </summary>
        /// <remarks>
        /// Returns as soon as the write is queued. It waits only when the queue is full, which bounds
        /// memory by making callers slow down rather than letting the backlog grow.
        /// </remarks>
        /// <param name="cacheKey">The cache key to persist under.</param>
        /// <param name="resources">The resources to persist. An empty list is a no-op.</param>
        /// <param name="resourceType">The FHIR type shared by every resource.</param>
        /// <param name="cancellationToken">Cancels waiting for queue space.</param>
        Task EnqueueAsync(
            string cacheKey,
            List<DomainResource> resources,
            ResourceType resourceType,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Waits until every queued write for <paramref name="cacheKeys"/> has been persisted.
        /// </summary>
        /// <remarks>
        /// The durability barrier. Call it before marking work terminal or producing an event that
        /// names these keys, so a reader can never arrive after the cached copy is evicted but before
        /// the durable copy exists. Keys with nothing outstanding return immediately.
        /// </remarks>
        /// <param name="cacheKeys">The keys to wait on.</param>
        /// <param name="cancellationToken">Cancels the wait.</param>
        /// <exception cref="ResourceCacheDurabilityException">
        /// A write for one of these keys failed permanently. The caller must not advertise them.
        /// </exception>
        Task WaitForDurableAsync(IEnumerable<string> cacheKeys, CancellationToken cancellationToken = default);

        /// <summary>
        /// Waits until every queued write for <paramref name="correlationId"/> and its per-resource-type
        /// keys has been persisted.
        /// </summary>
        /// <remarks>
        /// The form the pipeline actually uses, because a caller knows its correlation but not which
        /// cache keys its own work happened to touch. Waiting on the whole correlation is a superset of
        /// that, which is what makes it a safe barrier.
        /// </remarks>
        /// <param name="correlationId">The correlation to wait on.</param>
        /// <param name="cancellationToken">Cancels the wait.</param>
        /// <exception cref="ResourceCacheDurabilityException">
        /// A write for one of the correlation's keys failed permanently.
        /// </exception>
        Task WaitForCorrelationAsync(string correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Discards queued writes for <paramref name="cacheKeys"/> and clears any recorded failure.
        /// </summary>
        /// <remarks>
        /// Called when the keys are being deleted, so that a write still sitting in the queue cannot
        /// recreate what was just removed. Writes already in progress are allowed to finish; the
        /// delete that follows removes whatever they wrote.
        /// </remarks>
        /// <param name="cacheKeys">The keys whose queued writes should be discarded.</param>
        void Cancel(IEnumerable<string> cacheKeys);
    }
}
