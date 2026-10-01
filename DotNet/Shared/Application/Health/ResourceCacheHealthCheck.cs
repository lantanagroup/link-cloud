using Azure.Storage.Blobs;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace LantanaGroup.Link.Shared.Application.Health
{
    public class ResourceCacheHealthCheck : IHealthCheck
    {
        private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(5);

        private readonly IRedisDatabase? _redisDatabase;
        private readonly ResourceCacheSettings _settings;

        public ResourceCacheHealthCheck(IOptions<ResourceCacheSettings> settings, IServiceProvider serviceProvider)
        {
            _settings = settings.Value;
            _redisDatabase = serviceProvider.GetService<IRedisDatabase>();
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(HealthCheckTimeout);

            try
            {
                switch (_settings.CacheImplementation)
                {
                    case ResourceCacheType.Redis:
                        return await CheckCacheAsync(CheckRedisAsync, "Redis", timeoutCts.Token);
                    case ResourceCacheType.ABS:
                        return await CheckCacheAsync(CheckBlobStorageAsync, "Blob storage", timeoutCts.Token);
                    default:
                        // Blob storage is the durable source, so it decides healthy or not. Redis is
                        // a cache in front of it: losing it makes every read slower, not broken, and
                        // reporting Unhealthy for that would have containers restarted over something
                        // the design is built to absorb.
                        var blobResult = await CheckCacheAsync(CheckBlobStorageAsync, "Blob storage", timeoutCts.Token);
                        if (blobResult.Status != HealthStatus.Healthy)
                        {
                            return blobResult;
                        }

                        var redisResult = await CheckCacheAsync(CheckRedisAsync, "Redis", timeoutCts.Token);
                        return redisResult.Status == HealthStatus.Healthy
                            ? redisResult
                            : HealthCheckResult.Degraded(
                                "Resource cache is serving reads from blob storage; Redis is unavailable.",
                                redisResult.Exception);
                }
            }

            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Failed to connect to resource cache", ex);
            }
        }

        private static async Task<HealthCheckResult> CheckCacheAsync(
            Func<CancellationToken, Task> check,
            string cacheName,
            CancellationToken cancellationToken)
        {
            try
            {
                await check(cancellationToken);
                return HealthCheckResult.Healthy();
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy($"Failed to connect to {cacheName} resource cache", ex);
            }
        }

        private async Task CheckRedisAsync(CancellationToken cancellationToken)
        {
            if (_redisDatabase is null)
            {
                throw new InvalidOperationException("Redis cache is not configured.");
            }

            await _redisDatabase.Database.PingAsync().WaitAsync(cancellationToken);
        }

        private async Task CheckBlobStorageAsync(CancellationToken cancellationToken)
        {
            var blobSettings = _settings.BlobStorage;
            var containerClient = new BlobContainerClient(blobSettings.ConnectionString, blobSettings.BlobContainerName);

            var exists = await containerClient.ExistsAsync(cancellationToken);
            if (!exists.Value)
            {
                throw new InvalidOperationException("Azure Blob Storage cache container does not exist.");
            }
        }
    }
}
