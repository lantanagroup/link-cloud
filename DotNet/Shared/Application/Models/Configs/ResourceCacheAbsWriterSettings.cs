namespace LantanaGroup.Link.Shared.Application.Models.Configs
{
    /// <summary>
    /// Tuning for the background writer that persists resource cache entries to blob storage.
    /// </summary>
    /// <remarks>
    /// Every value ships with a working default, so none of these need provisioning per environment.
    /// See docs-dev/resource-cache.md.
    /// </remarks>
    public class ResourceCacheAbsWriterSettings
    {
        /// <summary>
        /// The most writes that may be waiting to be persisted before callers are made to wait.
        /// Defaults to 1000.
        /// </summary>
        /// <remarks>
        /// A queued write holds its deserialized resources in memory until it is persisted, so this is
        /// a memory bound as much as a throughput one. When the queue is full a caller enqueuing a
        /// write waits for space, degrading toward synchronous behaviour rather than growing without
        /// limit.
        /// </remarks>
        public int QueueCapacity { get; set; } = 1000;

        /// <summary>
        /// How many blob writes may run at once, across distinct cache keys. Defaults to 8.
        /// </summary>
        /// <remarks>
        /// Writes to the same key are always serialized regardless of this value, because the blob
        /// write reads the key's id list before appending to it.
        /// </remarks>
        public int MaxConcurrency { get; set; } = 8;

        /// <summary>
        /// The most resources that may be folded into one blob write when several writes for the
        /// same key are outstanding. Defaults to 2000.
        /// </summary>
        /// <remarks>
        /// A worker that finds a key already being written hands its batch to the holder rather than
        /// waiting, and successive hand-offs merge. That is what stops one key monopolising the
        /// workers, but a dequeued batch no longer occupies a channel slot, so without a ceiling the
        /// merged batch would grow outside what <see cref="QueueCapacity"/> bounds. At the ceiling a
        /// worker waits for the key instead, which bounds what the writer holds at roughly
        /// (QueueCapacity + 2 x MaxConcurrency) x this value.
        /// </remarks>
        public int MaxCoalescedResources { get; set; } = 2000;

        /// <summary>
        /// How many times a failing blob write is retried before the key is treated as permanently
        /// failed. Defaults to 3.
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// The delay before the first retry, in milliseconds, doubling on each subsequent attempt.
        /// Defaults to 200.
        /// </summary>
        public int RetryBaseDelayMilliseconds { get; set; } = 200;

        /// <summary>
        /// How long shutdown waits for queued writes to finish, in seconds. Defaults to 30.
        /// </summary>
        /// <remarks>
        /// Writes still queued when this elapses are abandoned. Callers are not left believing the
        /// data is durable, because a caller only advertises cache keys after waiting on them.
        /// </remarks>
        public int DrainTimeoutSeconds { get; set; } = 30;
    }
}
