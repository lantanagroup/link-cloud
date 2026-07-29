using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.ResourceCache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.System.Text.Json;

namespace LantanaGroup.Link.Shared.Application.Extensions
{
    public static class ResourceCacheExtensions
    {
        /// <summary>
        /// Registers an <see cref="IResourceCache"/> implementation based on the
        /// <c>ResourceCache:CacheImplementation</c> configuration value.
        /// <list type="bullet">
        ///   <item><description>
        ///     <see cref="ResourceCacheType.Hybrid"/> (default) — registers
        ///     <see cref="HybridResourceCache"/>, which dynamically selects Redis or ABS per
        ///     correlationId based on Redis memory pressure. Both Redis and BlobStorage must be
        ///     configured.
        ///   </description></item>
        ///   <item><description>
        ///     <see cref="ResourceCacheType.Redis"/> — registers <see cref="RedisResourceCache"/>
        ///     directly. Only Redis must be configured; BlobStorage is not required.
        ///   </description></item>
        ///   <item><description>
        ///     <see cref="ResourceCacheType.ABS"/> — registers <see cref="ABSResourceCache"/>
        ///     directly. Only BlobStorage must be configured; Redis is not required.
        ///   </description></item>
        /// </list>
        /// </summary>
        /// <remarks>
        /// Expects a config section named <c>ResourceCache</c> with the shape:
        /// <code>
        /// "ResourceCache": {
        ///   "CacheImplementation": "Hybrid",
        ///   "Redis": { "ConnectionString": "", "Password": "", "PoolSize": 5, "MemoryThresholdPercent": 80.0 },
        ///   "BlobStorage": { "ConnectionString": "", "BlobContainerName": "", "BlobRoot": "" }
        /// }
        /// </code>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the required connection settings for the selected implementation are missing.
        /// </exception>
        public static IServiceCollection AddResourceCache(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var section = configuration.GetSection(ResourceCacheSettings.SectionName);
            services.Configure<ResourceCacheSettings>(section);

            var settings = section.Get<ResourceCacheSettings>() ?? new ResourceCacheSettings();

            switch (settings.CacheImplementation)
            {
                case ResourceCacheType.Redis:
                    ValidateRedisSettings(settings.Redis);
                    RegisterRedisConnectionPool(services, settings.Redis);
                    services.AddSingleton<IResourceCache, RedisResourceCache>();
                    break;

                case ResourceCacheType.ABS:
                    ValidateBlobStorageSettings(settings.BlobStorage);
                    services.Configure<ResourceCacheBlobStorageSettings>(section.GetSection("BlobStorage"));
                    services.AddSingleton<IResourceCache, ABSResourceCache>();
                    break;

                default: // Hybrid
                    ValidateRedisSettings(settings.Redis);
                    ValidateBlobStorageSettings(settings.BlobStorage);
                    RegisterRedisConnectionPool(services, settings.Redis);
                    services.Configure<ResourceCacheBlobStorageSettings>(section.GetSection("BlobStorage"));
                    services.AddKeyedSingleton<IResourceCache, RedisResourceCache>(ResourceCacheType.Redis);
                    services.AddKeyedSingleton<IResourceCache, ABSResourceCache>(ResourceCacheType.ABS);
                    services.AddSingleton<IResourceCache, HybridResourceCache>();
                    break;
            }

            return services;
        }

        private static void RegisterRedisConnectionPool(IServiceCollection services, ResourceCacheRedisSettings redisSettings)
        {
            var connectionString = redisSettings.ConnectionString
                ?? throw new InvalidOperationException("ResourceCache:Redis:ConnectionString must be configured.");
            var configurationOptions = ConfigurationOptions.Parse(connectionString);
            configurationOptions.Password = redisSettings.Password;

            services.AddStackExchangeRedisExtensions<SystemTextJsonSerializer>(new StackExchange.Redis.Extensions.Core.Configuration.RedisConfiguration
            {
                ConnectionString = configurationOptions.ToString(true),
                PoolSize = redisSettings.PoolSize
            });
        }

        private static void ValidateRedisSettings(ResourceCacheRedisSettings redis)
        {
            if (string.IsNullOrEmpty(redis.ConnectionString))
                throw new InvalidOperationException(
                    $"ResourceCache:Redis:ConnectionString must be configured when using the {nameof(ResourceCacheType.Redis)} or {nameof(ResourceCacheType.Hybrid)} cache implementation.");
        }

        private static void ValidateBlobStorageSettings(ResourceCacheBlobStorageSettings blob)
        {
            if (string.IsNullOrEmpty(blob.ConnectionString))
                throw new InvalidOperationException(
                    $"ResourceCache:BlobStorage:ConnectionString must be configured when using the {nameof(ResourceCacheType.ABS)} or {nameof(ResourceCacheType.Hybrid)} cache implementation.");
        }
    }
}
