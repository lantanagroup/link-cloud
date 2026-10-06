namespace LantanaGroup.Link.Shared.Application.Interfaces
{
    /// <summary>
    /// Instruments the resource cache, so the cost of serving a read from the cache versus durable
    /// storage, and the cost of the durability barrier, are answerable from a dashboard.
    /// </summary>
    /// <remarks>
    /// <see cref="RecordDrainWait"/> is the one to watch after a change. Every other part of the
    /// durable write overlaps the acquisition paging loop; the barrier is the part that does not, so
    /// it is the honest measure of what durability costs the pipeline. See docs-dev/resource-cache.md.
    /// </remarks>
    public interface IResourceCacheMetrics
    {
        /// <summary>
        /// Records how long a read took and where it was ultimately served from.
        /// </summary>
        /// <param name="outcome">
        /// <see cref="ResourceCacheOutcomes.Hit"/>, <see cref="ResourceCacheOutcomes.Fallback"/> or
        /// <see cref="ResourceCacheOutcomes.Empty"/>. The ratio of hits to fallbacks is the headline
        /// number: it says whether Redis is actually earning its place.
        /// </param>
        /// <param name="fallbackReason">
        /// Why the cache did not serve it, from <see cref="ResourceCacheFallbackReasons"/>, or null on
        /// a hit. Carried on <see cref="ResourceCacheOutcomes.Empty"/> as well as
        /// <see cref="ResourceCacheOutcomes.Fallback"/>, because an empty result that followed a Redis
        /// outage is not the same answer as one that followed a plain miss. MeasureEval tags the same
        /// instrument the same way; the one rule both runtimes must keep is that the tag is *omitted*
        /// on a hit rather than recorded empty, since those export as different series.
        /// </param>
        /// <param name="milliseconds">Elapsed time for the whole read, including any fallback.</param>
        void RecordRead(string outcome, string? fallbackReason, double milliseconds);

        /// <summary>
        /// Counts reads that served a non-empty cache entry without checking whether it was whole,
        /// because its recorded durable count could not be used.
        /// </summary>
        /// <remarks>
        /// The partial-entry check is what stops an entry recreated after an eviction being served as
        /// the whole record. This counts the reads where that check did not run, so it should sit at
        /// zero; anything else means entries are being trusted unverified.
        /// <para>
        /// MeasureEval records the same instrument. The reachable causes differ because the count is
        /// fetched differently -- there the count rides the same round trip as the resources, so the
        /// only failure left is an unusable value, while here the read can also fail outright -- so the
        /// counter is defined by what it means rather than by its cause.
        /// </para>
        /// <para>
        /// MeasureEval also tags its copy with the evaluation pass. There is no equivalent here: the
        /// cache read path has no notion of a pass, and threading one through
        /// <see cref="IResourceCache.GetAsync"/> purely to tag a metric would invert the dependency.
        /// </para>
        /// </remarks>
        void IncrementDurableCountReadFailure();

        /// <summary>
        /// Records how long a write to one store took.
        /// </summary>
        /// <param name="store">
        /// <see cref="ResourceCacheStores.Redis"/> for the inline cache write, or
        /// <see cref="ResourceCacheStores.Blob"/> for the queued durable write.
        /// </param>
        /// <param name="outcome"><see cref="ResourceCacheOutcomes.Ok"/> or <see cref="ResourceCacheOutcomes.Failed"/>.</param>
        /// <param name="milliseconds">Elapsed time for that store's write.</param>
        void RecordWrite(string store, string outcome, double milliseconds);

        /// <summary>
        /// Records how long a durable write waited on the queue before it began.
        /// </summary>
        /// <remarks>
        /// Separates a backlog from slow storage: queue wait climbing while write duration holds
        /// steady means the writer is under-provisioned, not that blob storage has degraded.
        /// </remarks>
        void RecordQueueWait(double milliseconds);

        /// <summary>
        /// Records how long a caller blocked on the durability barrier.
        /// </summary>
        /// <remarks>
        /// Near zero means the queue drained while the caller was still doing other work, which is the
        /// design working. Approaching the write duration means the overlap is not happening.
        /// </remarks>
        void RecordDrainWait(double milliseconds);

        /// <summary>
        /// Counts a durable write that had to be retried, or that gave up.
        /// </summary>
        /// <param name="outcome">
        /// <see cref="ResourceCacheOutcomes.Retried"/> or <see cref="ResourceCacheOutcomes.Exhausted"/>.
        /// </param>
        void IncrementWriteRetry(string outcome);

        /// <summary>
        /// Supplies the callback that reports how many durable writes are outstanding.
        /// </summary>
        /// <remarks>
        /// Called once by the background writer at construction. A queue depth pinned at its bound is
        /// the earliest signal that durable storage cannot keep up with acquisition.
        /// </remarks>
        void TrackQueueDepth(Func<int> queueDepth);
    }
}
