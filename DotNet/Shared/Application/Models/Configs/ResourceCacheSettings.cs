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
        public int PoolSize { get; set; } = 5;
        /// <summary>
        /// The percentage of the configured Redis max-memory (<see cref="MaxMemoryBytes"/>) at
        /// which Hybrid caching falls back to ABS. Defaults to 80. When
        /// <see cref="MaxMemoryBytes"/> is unknown (null or &lt;= 0) Redis is always used.
        /// </summary>
        public double MemoryThresholdPercent { get; set; } = 80.0;

        /// <summary>
        /// The Redis max-memory limit in bytes, supplied via configuration because Azure
        /// Managed Redis does not return <c>maxmemory</c> via the INFO command. Used as the
        /// denominator when computing memory utilization for Hybrid cache fallback (the
        /// numerator, <c>used_memory</c>, is still read from INFO). When null or &lt;= 0 the
        /// limit is treated as unknown and Redis is always used (a warning is logged).
        /// NOTE: this must be updated manually if Redis capacity is scaled.
        /// </summary>
        public long? MaxMemoryBytes { get; set; }
    }
}
