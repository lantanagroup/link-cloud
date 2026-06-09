using LantanaGroup.Link.Shared.Application.Enums;

namespace LantanaGroup.Link.Shared.Application.Models.Configs
{
    public class ResourceCacheSettings
    {
        public const string SectionName = "ResourceCache";

        /// <summary>
        /// Selects which <see cref="IResourceCache"/> implementation is registered.
        /// Defaults to <see cref="ResourceCacheType.Hybrid"/>, which requires both Redis and
        /// BlobStorage to be configured and dynamically selects the backing store per
        /// correlationId based on Redis memory pressure.
        /// Use <see cref="ResourceCacheType.Redis"/> to use only Redis (BlobStorage not required).
        /// Use <see cref="ResourceCacheType.ABS"/> to use only Azure Blob Storage (Redis not required).
        /// </summary>
        public ResourceCacheType CacheImplementation { get; set; } = ResourceCacheType.Hybrid;

        public ResourceCacheRedisSettings Redis { get; set; } = new();
        public ResourceCacheBlobStorageSettings BlobStorage { get; set; } = new();
    }

    public class ResourceCacheRedisSettings
    {
        public string? ConnectionString { get; set; }
        public string? Password { get; set; }
        /// <summary>
        /// The percentage of maxmemory at which the cache will fall back to ABS.
        /// Defaults to 80. When Redis maxmemory is 0 (unlimited), Redis is always used.
        /// </summary>
        public double MemoryThresholdPercent { get; set; } = 80.0;
    }
}
