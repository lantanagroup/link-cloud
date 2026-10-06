namespace LantanaGroup.Link.Shared.Application.Models.ResourceCache
{
    /// <summary>
    /// Whether a cache key's recorded durable resource count can be used to judge the entry.
    /// </summary>
    /// <remarks>
    /// "Not recorded" and "unusable" were once the same answer, and a reader could not tell a key
    /// nothing had counted yet from one whose count had been corrupted. They lead to the same handling
    /// -- the entry is served -- but only one of them is a fault worth counting.
    /// </remarks>
    public enum DurableCountStatus
    {
        /// <summary>
        /// No count has been recorded for the key. Nothing has completed a durable write for it, so
        /// there is nothing to compare against and the entry is taken as it stands.
        /// </summary>
        NotRecorded,

        /// <summary>
        /// A count was recorded and can be compared against what the entry holds.
        /// </summary>
        Recorded,

        /// <summary>
        /// A count was recorded but cannot be read as a number, so the entry cannot be judged.
        /// </summary>
        Unusable
    }
}
