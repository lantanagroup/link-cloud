using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.ResourceCache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace LantanaGroup.Link.Shared.Application.Extensions
{
    public static class ResourceCacheExtensions
    {
        /// <summary>
        /// Registers both Redis and ABS resource cache implementations as keyed singletons,
        /// and registers <see cref="HybridResourceCache"/> as the <see cref="IResourceCache"/>
        /// singleton. The hybrid implementation selects the appropriate cache per correlationId
        /// based on Redis memory pressure at write time.
        /// </summary>
        /// <remarks>
        /// Expects a config section named <c>ResourceCache</c> with the shape:
        /// <code>
        /// "ResourceCache": {
        ///   "Redis": { "ConnectionString": "", "Password": "", "MemoryThresholdPercent": 80.0 },
        ///   "BlobStorage": { "ConnectionString": "", "BlobContainerName": "", "BlobRoot": "" }
        /// }
        /// </code>
        /// An <see cref="IConnectionMultiplexer"/> will be registered if one has not already
        /// been added (e.g. by <c>DistributedLockBuildAndAddToDI</c>).
        /// </remarks>
        public static IServiceCollection AddResourceCache(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var section = configuration.GetSection(ResourceCacheSettings.SectionName);
            services.Configure<ResourceCacheSettings>(section);

            var redisSettings = section.GetSection("Redis").Get<ResourceCacheRedisSettings>()
                ?? new ResourceCacheRedisSettings();

            // Only register IConnectionMultiplexer if no other registration is present.
            services.TryAddSingleton<IConnectionMultiplexer>(_ =>
            {
                var configOptions = new ConfigurationOptions
                {
                    AbortOnConnectFail = false,
                    EndPoints = { redisSettings.ConnectionString ?? string.Empty },
                    AllowAdmin = true, // Required to access INFO command for memory checks.
                };

                if (!string.IsNullOrEmpty(redisSettings.Password))
                {
                    configOptions.Password = redisSettings.Password;
                }

                return ConnectionMultiplexer.Connect(configOptions);
            });

            // Also make IOptions<ResourceCacheBlobStorageSettings> available for ABSResourceCache.
            services.Configure<ResourceCacheBlobStorageSettings>(section.GetSection("BlobStorage"));

            // Keyed registrations so HybridResourceCache (and Normalization's listener) can
            // resolve the concrete implementation by cache type.
            services.AddKeyedSingleton<IResourceCache, RedisResourceCache>(ResourceCacheType.Redis);
            services.AddKeyedSingleton<IResourceCache, ABSResourceCache>(ResourceCacheType.ABS);

            // The non-keyed IResourceCache resolves to the hybrid wrapper.
            services.AddSingleton<IResourceCache, HybridResourceCache>();

            return services;
        }
    }
}
