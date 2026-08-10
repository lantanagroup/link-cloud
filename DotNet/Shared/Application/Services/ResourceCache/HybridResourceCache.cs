using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis.Extensions.Core.Abstractions;
using System.Collections.Concurrent;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Shared.Application.Services.ResourceCache
{
    /// <summary>
    /// Selects Redis or ABS per correlationId based on Redis memory usage at the time of the
    /// first write. The decision is recorded in a dictionary so that all subsequent operations
    /// for the same correlationId use the same implementation, and so that message-builders can
    /// embed the correct <see cref="ResourceCacheType"/> in the Kafka ResourcesAcquired event.
    /// </summary>
    public class HybridResourceCache : IResourceCache
    {
        private readonly IResourceCache _redisCache;
        private readonly IResourceCache _absCache;
        private readonly IRedisDatabase _redisDatabase;
        private readonly ResourceCacheSettings _settings;
        private readonly ILogger<HybridResourceCache> _logger;

        private readonly ConcurrentDictionary<string, ResourceCacheType> _correlationCacheTypes = new();

        /// <summary>
        /// Memory statistics pulled out of Redis <c>INFO memory</c> and echoed on every selection
        /// decision. <c>maxmemory</c> is included deliberately: Azure Managed Redis is not expected
        /// to report it (the reason LEGLINK-770 moved the limit into configuration), and logging it
        /// confirms whether that assumption still holds for a given instance.
        /// </summary>
        private static readonly string[] DiagnosticMemoryKeys =
        {
            "used_memory",
            "used_memory_human",
            "used_memory_rss",
            "used_memory_dataset",
            "used_memory_peak",
            "maxmemory",
            "maxmemory_human",
            "maxmemory_policy",
            "total_system_memory"
        };

        private int _infoSectionLogged;

        public HybridResourceCache(
            [FromKeyedServices(ResourceCacheType.Redis)] IResourceCache redisCache,
            [FromKeyedServices(ResourceCacheType.ABS)] IResourceCache absCache,
            IRedisDatabase redisDatabase,
            IOptions<ResourceCacheSettings> settings,
            ILogger<HybridResourceCache> logger)
        {
            _redisCache = redisCache ?? throw new ArgumentNullException(nameof(redisCache));
            _absCache = absCache ?? throw new ArgumentNullException(nameof(absCache));
            _redisDatabase = redisDatabase ?? throw new ArgumentNullException(nameof(redisDatabase));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task UpdateCorrelationCacheAsync(string correlationId, List<DomainResource> resources, ResourceType resourceType, CancellationToken cancellationToken = default)
        {
            var cache = await DetermineAndRecordCacheAsync(correlationId, cancellationToken);
            await cache.UpdateCorrelationCacheAsync(correlationId, resources, resourceType, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<List<DomainResource>> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            var cache = ResolveFromKey(cacheKey);
            return await cache.GetAsync(cacheKey, cancellationToken);
        }

        /// <inheritdoc/>
        public ResourceType GetResourceTypeByCacheKey(string cacheKey)
        {
            var cache = ResolveFromKey(cacheKey);
            return cache.GetResourceTypeByCacheKey(cacheKey);
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(List<string> cacheKeys, CancellationToken cancellationToken = default)
        {
            var byType = cacheKeys
                .GroupBy(k => _correlationCacheTypes.GetValueOrDefault(ExtractCorrelationId(k), ResourceCacheType.Redis));

            foreach (var group in byType)
            {
                var cache = group.Key == ResourceCacheType.ABS ? _absCache : _redisCache;
                await cache.DeleteAsync(group.ToList(), cancellationToken);
            }

            // Clean up recorded decisions for deleted keys
            foreach (var key in cacheKeys)
            {
                _correlationCacheTypes.TryRemove(ExtractCorrelationId(key), out _);
            }
        }

        /// <inheritdoc/>
        public ResourceCacheType GetCacheTypeForCorrelationId(string correlationId)
        {
            return _correlationCacheTypes.GetValueOrDefault(correlationId, ResourceCacheType.Redis);
        }

        /// <inheritdoc/>
        public IResourceCache GetImplementation(ResourceCacheType cacheType)
        {
            return cacheType == ResourceCacheType.ABS ? _absCache : _redisCache;
        }

        // -------------------------------------------------------------------------

        private async Task<IResourceCache> DetermineAndRecordCacheAsync(string correlationId, CancellationToken cancellationToken)
        {
            var key = ExtractCorrelationId(correlationId);
            if (!_correlationCacheTypes.TryGetValue(key, out var cacheType))
            {
                cacheType = await SelectCacheTypeAsync(cancellationToken);
                cacheType = _correlationCacheTypes.GetOrAdd(key, cacheType);
            }

            return cacheType == ResourceCacheType.ABS ? _absCache : _redisCache;
        }

        private IResourceCache ResolveFromKey(string cacheKey)
        {
            var correlationId = ExtractCorrelationId(cacheKey);
            var cacheType = _correlationCacheTypes.GetValueOrDefault(correlationId, ResourceCacheType.Redis);
            return cacheType == ResourceCacheType.ABS ? _absCache : _redisCache;
        }

        /// <remarks>
        /// Runs once per correlationId (memoized by <see cref="DetermineAndRecordCacheAsync"/>), not
        /// per resource, so logging here is roughly once per patient-correlation and is safe at
        /// Information. Every path is logged at Information or above on purpose: the Redis-vs-ABS
        /// decision is otherwise invisible in deployed environments, which run at Information and
        /// therefore cannot distinguish "Redis is genuinely under pressure" from "the memory probe
        /// failed" (LEGLINK-948).
        /// </remarks>
        private async Task<ResourceCacheType> SelectCacheTypeAsync(CancellationToken cancellationToken)
        {
            var endpoint = "unknown";

            try
            {
                var multiplexer = _redisDatabase.Database.Multiplexer;
                var server = multiplexer.GetServers().FirstOrDefault(s => s.IsConnected);

                if (server == null)
                {
                    _logger.LogWarning(
                        "Redis memory probe found no connected server; using ABS resource cache. " +
                        "Configured endpoints: [{ConfiguredEndpoints}]. Server states: [{ServerStates}].",
                        DescribeConfiguredEndpoints(multiplexer),
                        DescribeServerStates(multiplexer));
                    return ResourceCacheType.ABS;
                }

                endpoint = server.EndPoint?.ToString() ?? "unknown";

                var memoryInfo = (await server.InfoAsync("memory").WaitAsync(cancellationToken)).FirstOrDefault();

                if (memoryInfo == null)
                {
                    _logger.LogWarning(
                        "Redis INFO memory returned no results from {Endpoint}; using ABS resource cache.",
                        endpoint);
                    return ResourceCacheType.ABS;
                }

                var infoDict = BuildInfoDictionary(memoryInfo);
                LogRawInfoSectionOnce(endpoint, infoDict);

                if (!infoDict.TryGetValue("used_memory", out var usedMemoryStr) ||
                    !long.TryParse(usedMemoryStr, out var usedMemory))
                {
                    _logger.LogWarning(
                        "Redis INFO memory from {Endpoint} has no parsable used_memory (raw value '{UsedMemoryRaw}'); " +
                        "using Redis resource cache. Reported memory: [{MemoryDiagnostics}].",
                        endpoint,
                        usedMemoryStr ?? "<absent>",
                        FormatDiagnostics(infoDict));
                    return ResourceCacheType.Redis;
                }

                // Azure Managed Redis does not return `maxmemory` via INFO, so the limit is
                // supplied through configuration instead. Continue reading `used_memory` above.
                var maxMemoryBytes = _settings.Redis.MaxMemoryBytes;
                if (maxMemoryBytes is null or <= 0)
                {
                    _logger.LogWarning(
                        "ResourceCache:Redis:MaxMemoryBytes is not configured or invalid ({MaxMemoryBytes}); " +
                        "cannot evaluate Redis memory pressure on {Endpoint}. Using Redis resource cache. " +
                        "Reported memory: [{MemoryDiagnostics}].",
                        maxMemoryBytes,
                        endpoint,
                        FormatDiagnostics(infoDict));
                    return ResourceCacheType.Redis;
                }

                double usagePercent = (double)usedMemory / maxMemoryBytes.Value * 100.0;
                var threshold = _settings.Redis.MemoryThresholdPercent;
                var selected = usagePercent >= threshold ? ResourceCacheType.ABS : ResourceCacheType.Redis;

                _logger.LogInformation(
                    "Redis memory probe on {Endpoint}: used_memory={UsedMemoryBytes} bytes of configured " +
                    "MaxMemoryBytes={MaxMemoryBytes} = {UsagePercent:F1}% against threshold {Threshold}% " +
                    "=> selected {CacheType} resource cache. Server-reported memory: [{MemoryDiagnostics}].",
                    endpoint,
                    usedMemory,
                    maxMemoryBytes.Value,
                    usagePercent,
                    threshold,
                    selected,
                    FormatDiagnostics(infoDict));

                return selected;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error checking Redis memory on {Endpoint}; falling back to ABS resource cache.",
                    endpoint);
                return ResourceCacheType.ABS;
            }
        }

        /// <summary>
        /// Builds a lookup from an INFO section without <see cref="Enumerable.ToDictionary{TSource,TKey,TElement}(IEnumerable{TSource},Func{TSource,TKey},Func{TSource,TElement})"/>,
        /// which throws on duplicate keys. Azure Managed Redis proxies Redis Enterprise and is not
        /// guaranteed to return the same unique-key INFO shape as open-source Redis; a duplicate key
        /// would otherwise surface as an opaque exception and silently force the ABS fallback.
        /// </summary>
        private static Dictionary<string, string> BuildInfoDictionary(
            IGrouping<string, KeyValuePair<string, string>> memoryInfo)
        {
            var infoDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in memoryInfo)
            {
                infoDict[entry.Key] = entry.Value;
            }

            return infoDict;
        }

        private static string FormatDiagnostics(Dictionary<string, string> infoDict)
        {
            return string.Join(
                ", ",
                DiagnosticMemoryKeys
                    .Where(infoDict.ContainsKey)
                    .Select(key => $"{key}={infoDict[key]}"));
        }

        /// <summary>
        /// Dumps the complete INFO memory section once per process. The curated
        /// <see cref="DiagnosticMemoryKeys"/> subset rides every decision; this exists so the raw
        /// server output can be inspected without shell access to the Redis instance, which is what
        /// distinguishes a mis-sized denominator from a metric that does not mean what we assume.
        /// </summary>
        private void LogRawInfoSectionOnce(string endpoint, Dictionary<string, string> infoDict)
        {
            if (Interlocked.Exchange(ref _infoSectionLogged, 1) != 0)
            {
                return;
            }

            _logger.LogInformation(
                "Redis INFO memory section from {Endpoint} (logged once per process to diagnose Hybrid " +
                "cache selection): [{InfoSection}].",
                endpoint,
                string.Join("; ", infoDict.Select(entry => $"{entry.Key}={entry.Value}")));
        }

        private static string DescribeConfiguredEndpoints(StackExchange.Redis.IConnectionMultiplexer multiplexer)
        {
            return string.Join(", ", multiplexer.GetEndPoints().Select(e => e.ToString()));
        }

        private static string DescribeServerStates(StackExchange.Redis.IConnectionMultiplexer multiplexer)
        {
            return string.Join(
                ", ",
                multiplexer.GetServers().Select(s => $"{s.EndPoint}: IsConnected={s.IsConnected}"));
        }

        private static string ExtractCorrelationId(string cacheKey)
        {
            var idx = cacheKey.IndexOf(':');
            return idx > 0 ? cacheKey[..idx] : cacheKey;
        }
    }
}
