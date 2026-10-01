using LantanaGroup.Link.Shared.Application.Enums;

namespace LantanaGroup.Link.Shared.Application.Models.Configs
{
    public class ResourceCacheSettings
    {
        public const string SectionName = "ResourceCache";

        /// <summary>
        /// Selects which <see cref="IResourceCache"/> implementation is registered.
        /// Defaults to <see cref="ResourceCacheType.Hybrid"/>, which requires both Redis and
        /// BlobStorage: blob storage is the durable copy of every entry and Redis is a read cache
        /// in front of it.
        /// Use <see cref="ResourceCacheType.Redis"/> to use only Redis (BlobStorage not required).
        /// Use <see cref="ResourceCacheType.ABS"/> to use only Azure Blob Storage (Redis not required).
        /// </summary>
        public ResourceCacheType CacheImplementation { get; set; } = ResourceCacheType.Hybrid;

        public ResourceCacheRedisSettings Redis { get; set; } = new();
        public ResourceCacheBlobStorageSettings BlobStorage { get; set; } = new();

        /// <summary>
        /// Tuning for the background writer that persists entries to blob storage. Only used by the
        /// <see cref="ResourceCacheType.Hybrid"/> implementation.
        /// </summary>
        public ResourceCacheAbsWriterSettings AbsWriter { get; set; } = new();
    }

    public class ResourceCacheRedisSettings
    {
        public string? ConnectionString { get; set; }
        public string? Password { get; set; }
        public int PoolSize { get; set; } = 5;

        /// <summary>
        /// The number of days Redis resource-cache entries remain after their most recent write.
        /// Defaults to 7. HybridResourceCache also uses this as the sliding lifetime of its
        /// in-process Redis-vs-ABS memo so that mapping cannot outlive the cache entries it describes.
        /// </summary>
        public int CacheEntryTtlDays { get; set; } = 7;
    }
}
