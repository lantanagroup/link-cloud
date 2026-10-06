namespace LantanaGroup.Link.Shared.Application.Services.ResourceCache
{
    /// <summary>
    /// What the partial-entry check made of a non-empty cache entry.
    /// </summary>
    /// <remarks>
    /// Only <see cref="Partial"/> rejects the entry. The other two both serve it, and are separate so
    /// that an entry served without the check can be counted.
    /// </remarks>
    internal enum CacheEntryState
    {
        /// <summary>The entry holds at least what durable storage recorded, or nothing recorded a count.</summary>
        Whole,

        /// <summary>The entry holds fewer resources than durable storage recorded.</summary>
        Partial,

        /// <summary>The recorded count could not be used, so the entry was served unchecked.</summary>
        CountUnusable
    }
}
