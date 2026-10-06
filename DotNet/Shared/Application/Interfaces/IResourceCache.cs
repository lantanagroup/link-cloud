using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.ResourceCache;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Shared.Application.Interfaces
{
    /// <summary>
    /// Stores FHIR resource bodies for a correlation so that Kafka events can carry cache pointers
    /// rather than payloads.
    /// </summary>
    /// <remarks>
    /// Keys come in two generations. Acquisition writes one key per resource type as
    /// <c>{correlationId}:{ResourceType}</c>. Normalization accumulates its results into the bare
    /// <c>{correlationId}</c> key and then deletes the acquisition keys, and MeasureEval reads only
    /// that bare key. See docs-dev/resource-cache.md.
    /// </remarks>
    public interface IResourceCache
    {
        /// <summary>
        /// Reads every resource stored under <paramref name="cacheKey"/>.
        /// </summary>
        /// <remarks>
        /// Returns an empty list when the key holds nothing, rather than throwing; the caller decides
        /// whether an empty result is a failure. A resource that cannot be deserialized is logged and
        /// skipped so that one bad entry does not fail the whole read.
        /// </remarks>
        /// <param name="cacheKey">
        /// Either <c>{correlationId}</c> or <c>{correlationId}:{ResourceType}</c>.
        /// </param>
        /// <param name="cancellationToken">Cancels the read.</param>
        /// <returns>
        /// The resources stored under the key, or an empty list.
        /// </returns>
        Task<List<DomainResource>> GetAsync(string cacheKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes <paramref name="cacheKeys"/> and everything stored under them.
        /// </summary>
        /// <remarks>
        /// Deleting a key that does not exist is not an error. Callers are the success path in
        /// Normalization, which drops the acquisition keys once it has written the normalized
        /// generation, and the terminal-failure purge. Never call it on a retryable failure: a
        /// redelivered message still needs the resources it names.
        /// </remarks>
        /// <param name="cacheKeys">The keys to remove.</param>
        /// <param name="cancellationToken">Cancels the delete.</param>
        Task DeleteAsync(List<string> cacheKeys, CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces everything stored for <paramref name="cacheKey"/> with <paramref name="resources"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="AppendResourcesAsync"/> merges, so it cannot remove anything. Removing
        /// requires replacing, and replacing is not a delete followed by a write: between those two
        /// steps the key holds nothing, and a delete whose failure is tolerated leaves the old content
        /// for the write to merge back into. So this is one operation. The cache does it atomically, the
        /// durable store is written before the cache so a failure leaves the cache holding the only copy
        /// for the retry to work from, and a failure anywhere is raised rather than tolerated -- a
        /// replace that cannot clear is not a replace.
        /// <para>
        /// An empty <paramref name="resources"/> removes the key.
        /// </para>
        /// </remarks>
        /// <param name="cacheKey">The key to replace.</param>
        /// <param name="resources">The resources the key should hold afterwards.</param>
        /// <param name="resourceType">The resource type being stored.</param>
        /// <param name="cancellationToken">Cancels the replace.</param>
        Task ReplaceResourcesAsync(
            string cacheKey,
            List<DomainResource> resources,
            ResourceType resourceType,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds resources to <paramref name="cacheKey"/>, merging with whatever is already there.
        /// </summary>
        /// <remarks>
        /// Additive, never replacing, so a resource type acquired across several sibling query logs
        /// accumulates into one key. Resources already present under the key are not written twice.
        /// Because it only ever adds, removing entries needs
        /// <see cref="ReplaceResourcesAsync"/> rather than this, which is what the non-org
        /// encounter strip uses.
        /// </remarks>
        /// <param name="cacheKey">
        /// The key to write: either <c>{correlationId}</c> or <c>{correlationId}:{ResourceType}</c>.
        /// </param>
        /// <param name="resources">The resources to add. An empty list is a no-op.</param>
        /// <param name="resourceType">
        /// The FHIR type shared by every resource in <paramref name="resources"/>.
        /// </param>
        /// <param name="cancellationToken">Cancels the write.</param>
        Task AppendResourcesAsync(string cacheKey, List<DomainResource> resources, ResourceType resourceType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Parses the FHIR resource type out of a <c>{correlationId}:{ResourceType}</c> cache key.
        /// </summary>
        /// <param name="cacheKey">A key carrying a resource-type segment.</param>
        /// <returns>The resource type named by the key's second segment.</returns>
        /// <exception cref="Exception">
        /// The key has no <c>:</c> separator, or the segment after it is not a FHIR resource type.
        /// </exception>
        ResourceType GetResourceTypeByCacheKey(string cacheKey);

        /// <summary>
        /// Waits until everything written for <paramref name="correlationId"/> has reached durable
        /// storage.
        /// </summary>
        /// <remarks>
        /// The barrier that makes a correlation's cache keys safe to advertise. Call it before marking
        /// work terminal, and before producing an event that names those keys: writes reach the cache
        /// immediately but durable storage on a background queue, so without it a reader can arrive
        /// after the cached copy is evicted and before the durable copy exists.
        /// <para>
        /// Waits only on the calling process's queue. Implementations that write durably in-line
        /// complete immediately.
        /// </para>
        /// </remarks>
        /// <param name="correlationId">The correlation to wait on.</param>
        /// <param name="cancellationToken">Cancels the wait.</param>
        /// <exception cref="ResourceCacheDurabilityException">
        /// A durable write for this correlation failed permanently, so its keys must not be advertised.
        /// </exception>
        Task WaitForDurableAsync(string correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Blocks until every durable write for <paramref name="cacheKeys"/> has landed.
        /// </summary>
        /// <remarks>
        /// Prefer this over the correlation-wide overload when the caller owns only part of a
        /// correlation. A failure is reported to every waiter on the key until the resources it covers
        /// are written again, so this fails whenever any write to these keys did -- including a
        /// sibling's. A caller that knows which resources it wrote should use the overload that takes
        /// them.
        /// </remarks>
        /// <param name="cacheKeys">The keys to wait on.</param>
        /// <param name="cancellationToken">Cancels the wait.</param>
        /// <exception cref="ResourceCacheDurabilityException">
        /// A durable write for one of these keys failed permanently, so they must not be advertised.
        /// </exception>
        Task WaitForDurableAsync(IEnumerable<string> cacheKeys, CancellationToken cancellationToken = default);

        /// <summary>
        /// Blocks until every durable write for <paramref name="cacheKeys"/> has landed, and fails only
        /// if one of <paramref name="references"/> did not.
        /// </summary>
        /// <remarks>
        /// For a caller that shares keys with other work, such as sibling acquisition logs writing the
        /// same resource type. Several of them can wait on one key, and a hand-off can merge their
        /// resources into one durable write, so the key alone cannot say whose write failed. Scoping
        /// the answer to the caller's own resources tells the log that owns a failure, and only that
        /// log.
        /// </remarks>
        /// <param name="cacheKeys">The keys to wait on.</param>
        /// <param name="references">The caller's resources, as <c>{ResourceType}/{id}</c>.</param>
        /// <param name="cancellationToken">Cancels the wait.</param>
        /// <exception cref="ResourceCacheDurabilityException">
        /// A durable write holding one of <paramref name="references"/> failed permanently, so these
        /// keys must not be advertised.
        /// </exception>
        Task WaitForDurableAsync(IEnumerable<string> cacheKeys,
                                 IReadOnlyCollection<string> references,
                                 CancellationToken cancellationToken = default);

        /// <summary>
        /// True when the backing store has at least one resource for <paramref name="cacheKey"/>,
        /// without deserializing FHIR payloads.
        /// </summary>
        /// <param name="cacheKey">The key to test.</param>
        /// <param name="cancellationToken">Cancels the check.</param>
        /// <returns>True when the key holds at least one resource.</returns>
        Task<bool> HasResourcesAsync(string cacheKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// How many resources this store holds for <paramref name="cacheKey"/>, without deserializing
        /// FHIR payloads.
        /// </summary>
        /// <param name="cacheKey">The key to count.</param>
        /// <param name="cancellationToken">Cancels the count.</param>
        /// <returns>The number of resources held, or zero when the key is absent.</returns>
        Task<int> GetResourceCountAsync(string cacheKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// The resource count the durable store is known to hold for <paramref name="cacheKey"/>.
        /// </summary>
        /// <remarks>
        /// A cache write is a merge that recreates a key the cache has evicted, so an entry holding only
        /// the most recent batch is non-empty and otherwise indistinguishable from a whole one. Comparing
        /// the count this returns against <see cref="GetResourceCountAsync"/> is what tells them apart.
        /// A count nothing has recorded yet is unknown rather than agreement, and a count that cannot be
        /// read is a fault rather than an absence -- both serve the entry, but only one is worth
        /// counting, so they are distinct statuses rather than one null. See docs-dev/resource-cache.md.
        /// </remarks>
        /// <param name="cacheKey">The key to read the recorded count for.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        /// <returns>The recorded count, or the status saying why there is none to compare against.</returns>
        Task<DurableResourceCount> GetDurableResourceCountAsync(string cacheKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Whether a read of <paramref name="cacheKey"/> would be served the whole record without
        /// going to durable storage.
        /// </summary>
        /// <remarks>
        /// The question <see cref="GetAsync"/> answers on the way past, exposed on its own so a caller
        /// that only wants the entry made whole can skip deserializing an entry that already is. False
        /// when the answer cannot be determined, so the caller reads through and the uncertainty costs
        /// a read rather than a wrong answer. Always true for a store that nothing shadows.
        /// </remarks>
        /// <param name="cacheKey">The key to test.</param>
        /// <param name="cancellationToken">Cancels the reads this makes.</param>
        Task<bool> IsEntryCompleteAsync(string cacheKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Records how many resources durable storage holds for <paramref name="cacheKey"/>.
        /// </summary>
        /// <remarks>
        /// Called once a durable write has landed, so the value describes storage that a reader can rely
        /// on. Stores that are themselves durable have nothing to record and ignore it.
        /// </remarks>
        /// <param name="cacheKey">The key the count belongs to.</param>
        /// <param name="count">The number of resources durable storage holds.</param>
        /// <param name="cancellationToken">Cancels the write.</param>
        Task SetDurableResourceCountAsync(string cacheKey, int count, CancellationToken cancellationToken = default);
    }
}
