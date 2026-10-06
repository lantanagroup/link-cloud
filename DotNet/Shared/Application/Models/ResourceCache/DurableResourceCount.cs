namespace LantanaGroup.Link.Shared.Application.Models.ResourceCache
{
    /// <summary>
    /// The resource count durable storage is recorded as holding for one cache key.
    /// </summary>
    /// <remarks>
    /// <see cref="Count"/> is meaningful only when <see cref="Status"/> is
    /// <see cref="DurableCountStatus.Recorded"/>. See docs-dev/resource-cache.md.
    /// </remarks>
    /// <param name="Status">Whether the count can be used.</param>
    /// <param name="Count">The recorded count, when there is one.</param>
    public readonly record struct DurableResourceCount(DurableCountStatus Status, int Count)
    {
        /// <summary>
        /// No count has been recorded for the key.
        /// </summary>
        public static DurableResourceCount NotRecorded { get; } = new(DurableCountStatus.NotRecorded, 0);

        /// <summary>
        /// A count was recorded but could not be read as a number.
        /// </summary>
        public static DurableResourceCount Unusable { get; } = new(DurableCountStatus.Unusable, 0);

        /// <summary>
        /// A usable recorded count.
        /// </summary>
        /// <param name="count">The count durable storage holds.</param>
        public static DurableResourceCount Of(int count) => new(DurableCountStatus.Recorded, count);
    }
}
