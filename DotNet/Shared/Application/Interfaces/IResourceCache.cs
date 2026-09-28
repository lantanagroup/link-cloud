using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Enums;
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
        /// Adds resources to <paramref name="correlationId"/>, merging with whatever is already there.
        /// </summary>
        /// <remarks>
        /// Additive, never replacing, so a resource type acquired across several sibling query logs
        /// accumulates into one key. Resources already present under the key are not written twice.
        /// Because it only ever adds, removing entries requires a <see cref="DeleteAsync"/> followed by
        /// a rewrite of the survivors, which is how the non-org encounter strip works.
        /// </remarks>
        /// <param name="correlationId">
        /// The cache key to write, despite the name: either <c>{correlationId}</c> or
        /// <c>{correlationId}:{ResourceType}</c>.
        /// </param>
        /// <param name="resources">The resources to add. An empty list is a no-op.</param>
        /// <param name="resourceType">
        /// The FHIR type shared by every resource in <paramref name="resources"/>.
        /// </param>
        /// <param name="cancellationToken">Cancels the write.</param>
        Task UpdateCorrelationCacheAsync(string correlationId, List<DomainResource> resources, ResourceType resourceType, CancellationToken cancellationToken = default);

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
        /// The store holding <paramref name="correlationId"/>, consulting the shared Redis memo when
        /// this process has none of its own.
        /// </summary>
        /// <remarks>
        /// Removed by LEGLINK-1276 once the Hybrid cache writes every correlation to both stores:
        /// with nothing to choose between, there is nothing to report. It survives for now only
        /// because it still stamps the <c>CacheType</c> field that the current Hybrid's exclusive
        /// store selection makes meaningful.
        /// </remarks>
        /// <param name="correlationId">The correlation, or any cache key beginning with it.</param>
        /// <param name="cancellationToken">Cancels the memo lookup.</param>
        /// <returns>
        /// The recorded store, or <see cref="ResourceCacheType.Redis"/> when no memo exists anywhere.
        /// </returns>
        Task<ResourceCacheType> GetCacheTypeForCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// The concrete single-store cache behind <paramref name="cacheType"/>.
        /// </summary>
        /// <remarks>
        /// Removed by LEGLINK-1276. Callers use it to pin every operation for one message to the store
        /// named by its <c>CacheType</c> field; once reads fall back automatically there is no reason
        /// to reach past the configured cache.
        /// </remarks>
        /// <param name="cacheType">The store to resolve.</param>
        /// <returns>The implementation for that store.</returns>
        /// <exception cref="NotSupportedException">
        /// A single-store implementation was asked for a store other than its own.
        /// </exception>
        IResourceCache GetImplementation(ResourceCacheType cacheType);

        /// <summary>
        /// True when the backing store has at least one resource for <paramref name="cacheKey"/>,
        /// without deserializing FHIR payloads.
        /// </summary>
        /// <param name="cacheKey">The key to test.</param>
        /// <param name="cancellationToken">Cancels the check.</param>
        /// <returns>True when the key holds at least one resource.</returns>
        Task<bool> HasResourcesAsync(string cacheKey, CancellationToken cancellationToken = default);
    }
}
