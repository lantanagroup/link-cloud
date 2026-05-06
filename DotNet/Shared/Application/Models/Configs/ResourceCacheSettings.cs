namespace LantanaGroup.Link.Shared.Application.Models.Configs
{
    public class ResourceCacheSettings
    {
        public const string SectionName = "ResourceCache";

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
