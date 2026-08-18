using Azure.Storage.Blobs;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace LantanaGroup.Link.Shared.Application.Health
{
    public class ResourceCacheHealthCheck : IHealthCheck
    {
        private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(5);

        private readonly IConnectionMultiplexer? _redis;
        private readonly ResourceCacheSettings _settings;

        public ResourceCacheHealthCheck(IOptions<ResourceCacheSettings> settings, IServiceProvider serviceProvider)
        {
            _settings = settings.Value;
            _redis = serviceProvider.GetService<IConnectionMultiplexer>();
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
                        await CheckRedisAsync(timeoutCts.Token);
                        break;
                    case ResourceCacheType.ABS:
                        await CheckBlobStorageAsync(timeoutCts.Token);
                        break;
                    default:
                        await CheckRedisAsync(timeoutCts.Token);
                        await CheckBlobStorageAsync(timeoutCts.Token);
                        break;
                }

                return HealthCheckResult.Healthy();
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Failed to connect to resource cache", ex);
            }
        }

        private async Task CheckRedisAsync(CancellationToken cancellationToken)
        {
            if (_redis is null || !_redis.IsConnected)
            {
                throw new InvalidOperationException("Redis cache is not connected.");
            }

            await _redis.GetDatabase().PingAsync().WaitAsync(cancellationToken);
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
