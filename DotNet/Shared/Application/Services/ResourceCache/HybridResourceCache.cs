using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
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

        /// <summary>
        /// In-process Redis-vs-ABS memo. Entries are not removed by <see cref="DeleteAsync"/>:
        /// org-location strip deletes only <c>{correlation}:Encounter</c>, and remaining
        /// Patient/Location keys are purged by Normalization via <see cref="GetImplementation"/>,
        /// never through this Hybrid instance. Eviction is sliding TTL (same as Redis
        /// <see cref="ResourceCacheRedisSettings.CacheEntryTtlDays"/>) plus an explicit
        /// <see cref="ForgetCacheTypeForCorrelationId"/> after the ResourcesAcquired tail is produced.
        /// </summary>
        private readonly ConcurrentDictionary<string, CacheTypeEntry> _correlationCacheTypes = new();
        private readonly TimeProvider _timeProvider;
        private readonly TimeSpan _correlationCacheTypeTtl;
        private int _accessesSinceSweep;

        private const int SweepInterval = 256;

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
            : this(redisCache, absCache, redisDatabase, settings, logger, TimeProvider.System)
        {
        }

        public HybridResourceCache(
            [FromKeyedServices(ResourceCacheType.Redis)] IResourceCache redisCache,
            [FromKeyedServices(ResourceCacheType.ABS)] IResourceCache absCache,
            IRedisDatabase redisDatabase,
            IOptions<ResourceCacheSettings> settings,
            ILogger<HybridResourceCache> logger,
            TimeProvider timeProvider)
        {
            _redisCache = redisCache ?? throw new ArgumentNullException(nameof(redisCache));
            _absCache = absCache ?? throw new ArgumentNullException(nameof(absCache));
            _redisDatabase = redisDatabase ?? throw new ArgumentNullException(nameof(redisDatabase));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

            var ttlDays = _settings.Redis.CacheEntryTtlDays;
            _correlationCacheTypeTtl = TimeSpan.FromDays(ttlDays > 0 ? ttlDays : 7);
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
                .GroupBy(k => TryGetRecordedCacheType(ExtractCorrelationId(k), out var cacheType)
                    ? cacheType
                    : ResourceCacheType.Redis);

            foreach (var group in byType)
            {
                var cache = group.Key == ResourceCacheType.ABS ? _absCache : _redisCache;
                await cache.DeleteAsync(group.ToList(), cancellationToken);
            }

            // Keep the Redis-vs-ABS decision for the correlation. Deleting one resource-type
            // key (org-location Encounter strip) must not make later Patient/Location reads
            // fall back to Redis while the remaining blobs still live in ABS. Patient/Location
            // keys are later deleted by Normalization through GetImplementation, so last-key
            // tracking here would never see a complete eviction.
        }

        /// <inheritdoc/>
        public ResourceCacheType GetCacheTypeForCorrelationId(string correlationId)
        {
            return TryGetRecordedCacheType(ExtractCorrelationId(correlationId), out var cacheType)
                ? cacheType
                : ResourceCacheType.Redis;
        }

        /// <inheritdoc/>
        public IResourceCache GetImplementation(ResourceCacheType cacheType)
        {
            return cacheType == ResourceCacheType.ABS ? _absCache : _redisCache;
        }

        /// <inheritdoc/>
        public void ForgetCacheTypeForCorrelationId(string correlationId)
        {
            if (string.IsNullOrEmpty(correlationId))
            {
                return;
            }

            _correlationCacheTypes.TryRemove(ExtractCorrelationId(correlationId), out _);
        }

        // -------------------------------------------------------------------------

        private async Task<IResourceCache> DetermineAndRecordCacheAsync(string correlationId, CancellationToken cancellationToken)
        {
            var key = ExtractCorrelationId(correlationId);
            if (!TryGetRecordedCacheType(key, out var cacheType))
            {
                cacheType = await SelectCacheTypeAsync(cancellationToken);
                cacheType = RememberCacheType(key, cacheType);
            }

            return cacheType == ResourceCacheType.ABS ? _absCache : _redisCache;
        }

        private IResourceCache ResolveFromKey(string cacheKey)
        {
            var correlationId = ExtractCorrelationId(cacheKey);
            var cacheType = TryGetRecordedCacheType(correlationId, out var recorded)
                ? recorded
                : ResourceCacheType.Redis;
            return cacheType == ResourceCacheType.ABS ? _absCache : _redisCache;
        }

        private ResourceCacheType RememberCacheType(string correlationId, ResourceCacheType cacheType)
        {
            var nowTicks = _timeProvider.GetUtcNow().UtcTicks;
            var entry = _correlationCacheTypes.GetOrAdd(
                correlationId,
                _ => new CacheTypeEntry(cacheType, nowTicks));
            entry.LastAccessedUtcTicks = nowTicks;
            MaybeSweepExpired();
            return entry.Type;
        }

        private bool TryGetRecordedCacheType(string correlationId, out ResourceCacheType cacheType)
        {
            MaybeSweepExpired();

            if (_correlationCacheTypes.TryGetValue(correlationId, out var entry))
            {
                if (!IsExpired(entry))
                {
                    entry.LastAccessedUtcTicks = _timeProvider.GetUtcNow().UtcTicks;
                    cacheType = entry.Type;
                    return true;
                }

                _correlationCacheTypes.TryRemove(correlationId, out _);
            }

            cacheType = default;
            return false;
        }

        private bool IsExpired(CacheTypeEntry entry)
        {
            var age = _timeProvider.GetUtcNow().UtcTicks - entry.LastAccessedUtcTicks;
            return age >= _correlationCacheTypeTtl.Ticks;
        }

        private void MaybeSweepExpired()
        {
            if (Interlocked.Increment(ref _accessesSinceSweep) < SweepInterval)
            {
                return;
            }

            Interlocked.Exchange(ref _accessesSinceSweep, 0);

            var cutoff = _timeProvider.GetUtcNow().UtcTicks - _correlationCacheTypeTtl.Ticks;
            foreach (var pair in _correlationCacheTypes)
            {
                if (pair.Value.LastAccessedUtcTicks < cutoff)
                {
                    _correlationCacheTypes.TryRemove(pair.Key, out _);
                }
            }
        }

        private sealed class CacheTypeEntry
        {
            public CacheTypeEntry(ResourceCacheType type, long lastAccessedUtcTicks)
            {
                Type = type;
                LastAccessedUtcTicks = lastAccessedUtcTicks;
            }

            public ResourceCacheType Type { get; }
            public long LastAccessedUtcTicks;
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

                var reading = await ResolveUsedMemoryAsync(server, endpoint, cancellationToken);

                if (reading == null)
                {
                    _logger.LogWarning(
                        "Could not determine Redis used memory on {Endpoint} from INFO memory, MEMORY STATS or a " +
                        "full INFO dump; memory pressure cannot be evaluated. Using ABS resource cache.",
                        endpoint);
                    return ResourceCacheType.ABS;
                }

                // Azure Managed Redis does not return `maxmemory` via INFO, so the limit is
                // supplied through configuration instead. Only the numerator comes from the server.
                var maxMemoryBytes = _settings.Redis.MaxMemoryBytes;
                if (maxMemoryBytes is null or <= 0)
                {
                    _logger.LogWarning(
                        "ResourceCache:Redis:MaxMemoryBytes is not configured or invalid ({MaxMemoryBytes}); " +
                        "cannot evaluate Redis memory pressure on {Endpoint}. Using Redis resource cache. " +
                        "Used memory was {UsedMemoryBytes} bytes via {MemorySource}. Reported memory: [{MemoryDiagnostics}].",
                        maxMemoryBytes,
                        endpoint,
                        reading.UsedMemory,
                        reading.Source,
                        FormatDiagnostics(reading.Diagnostics));
                    return ResourceCacheType.Redis;
                }

                double usagePercent = (double)reading.UsedMemory / maxMemoryBytes.Value * 100.0;
                var threshold = _settings.Redis.MemoryThresholdPercent;
                var selected = usagePercent >= threshold ? ResourceCacheType.ABS : ResourceCacheType.Redis;

                _logger.LogInformation(
                    "Redis memory probe on {Endpoint}: used memory {UsedMemoryBytes} bytes (via {MemorySource}) of " +
                    "configured MaxMemoryBytes={MaxMemoryBytes} = {UsagePercent:F1}% against threshold {Threshold}% " +
                    "=> selected {CacheType} resource cache. Server-reported memory: [{MemoryDiagnostics}].",
                    endpoint,
                    reading.UsedMemory,
                    reading.Source,
                    maxMemoryBytes.Value,
                    usagePercent,
                    threshold,
                    selected,
                    FormatDiagnostics(reading.Diagnostics));

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

        /// <summary>A used-memory figure and the server command it was recovered from.</summary>
        private sealed record UsedMemoryReading(
            long UsedMemory,
            string Source,
            Dictionary<string, string> Diagnostics);

        /// <summary>
        /// Recovers a used-memory figure, trying progressively more general commands. Only the
        /// numerator is sourced from the server — the limit it is measured against comes from
        /// <c>ResourceCache:Redis:MaxMemoryBytes</c>, because Azure Managed Redis does not report
        /// <c>maxmemory</c> via INFO (LEGLINK-770). AMR proxies Redis Enterprise, so the same
        /// restrictions that hide <c>maxmemory</c> may hide <c>used_memory</c> or the whole INFO
        /// memory section; falling straight through to a cache choice on the first miss would
        /// decide on no evidence (LEGLINK-948).
        /// </summary>
        private async Task<UsedMemoryReading?> ResolveUsedMemoryAsync(
            IServer server,
            string endpoint,
            CancellationToken cancellationToken)
        {
            var infoDict = await ReadInfoAsync(server, "memory", cancellationToken);

            if (infoDict != null)
            {
                LogRawInfoSectionOnce(endpoint, infoDict);

                if (TryParseUsedMemory(infoDict, out var usedMemory))
                {
                    return new UsedMemoryReading(usedMemory, "INFO memory", infoDict);
                }

                _logger.LogWarning(
                    "Redis INFO memory from {Endpoint} has no parsable used_memory (raw value '{UsedMemoryRaw}'); " +
                    "trying MEMORY STATS. Reported memory: [{MemoryDiagnostics}].",
                    endpoint,
                    infoDict.GetValueOrDefault("used_memory") ?? "<absent>",
                    FormatDiagnostics(infoDict));
            }
            else
            {
                _logger.LogWarning(
                    "Redis INFO memory returned no results from {Endpoint}; trying MEMORY STATS.",
                    endpoint);
            }

            var diagnostics = infoDict ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var totalAllocated = await TryReadTotalAllocatedAsync(server, endpoint, cancellationToken);
            if (totalAllocated.HasValue)
            {
                return new UsedMemoryReading(totalAllocated.Value, "MEMORY STATS total.allocated", diagnostics);
            }

            var fullInfo = await ReadInfoAsync(server, section: null, cancellationToken);
            if (fullInfo != null && TryParseUsedMemory(fullInfo, out var fullUsedMemory))
            {
                return new UsedMemoryReading(fullUsedMemory, "INFO (full)", fullInfo);
            }

            return null;
        }

        /// <summary>
        /// Reads an INFO section (or the whole of INFO when <paramref name="section"/> is null) into
        /// a flat lookup. Built with an indexer rather than
        /// <see cref="Enumerable.ToDictionary{TSource,TKey,TElement}(IEnumerable{TSource},Func{TSource,TKey},Func{TSource,TElement})"/>,
        /// which throws on duplicate keys: Azure Managed Redis is not guaranteed to return the same
        /// unique-key INFO shape as open-source Redis, and a duplicate would otherwise surface as an
        /// opaque exception and silently force the ABS fallback.
        /// </summary>
        private async Task<Dictionary<string, string>?> ReadInfoAsync(
            IServer server,
            string? section,
            CancellationToken cancellationToken)
        {
            IGrouping<string, KeyValuePair<string, string>>[]? groups;

            try
            {
                groups = section == null
                    ? await server.InfoAsync().WaitAsync(cancellationToken)
                    : await server.InfoAsync(section).WaitAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A rejected or unsupported INFO is a reason to try another source, not a reason to
                // abandon the probe: letting this reach the outer catch would decide the cache on a
                // command restriction rather than on memory pressure.
                _logger.LogWarning(
                    ex,
                    "Redis INFO {Section} failed on {Endpoint}.",
                    section ?? "(full)",
                    server.EndPoint?.ToString() ?? "unknown");
                return null;
            }

            if (groups == null)
            {
                return null;
            }

            var infoDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                foreach (var entry in group)
                {
                    infoDict[entry.Key] = entry.Value;
                }
            }

            return infoDict.Count == 0 ? null : infoDict;
        }

        private static bool TryParseUsedMemory(Dictionary<string, string> infoDict, out long usedMemory)
        {
            usedMemory = 0;
            return infoDict.TryGetValue("used_memory", out var raw) && long.TryParse(raw, out usedMemory);
        }

        /// <summary>
        /// Reads <c>total.allocated</c> from MEMORY STATS as a second opinion when INFO does not
        /// yield <c>used_memory</c>. MEMORY STATS may itself be restricted, so failure is expected
        /// and non-fatal.
        /// </summary>
        private async Task<long?> TryReadTotalAllocatedAsync(
            IServer server,
            string endpoint,
            CancellationToken cancellationToken)
        {
            try
            {
                var stats = await server.MemoryStatsAsync().WaitAsync(cancellationToken);
                var totalAllocated = ReadTotalAllocated(stats);

                if (totalAllocated == null)
                {
                    _logger.LogWarning(
                        "MEMORY STATS on {Endpoint} did not report total.allocated; trying a full INFO dump.",
                        endpoint);
                }

                return totalAllocated;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "MEMORY STATS is unavailable on {Endpoint}; trying a full INFO dump.",
                    endpoint);
                return null;
            }
        }

        private static long? ReadTotalAllocated(RedisResult? stats)
        {
            if (stats == null || stats.IsNull)
            {
                return null;
            }

            RedisResult[]? entries;

            try
            {
                entries = (RedisResult[]?)stats;
            }
            catch (InvalidCastException)
            {
                // MEMORY STATS replies as a flat array; anything else is not something we can read.
                return null;
            }

            if (entries == null)
            {
                return null;
            }

            for (var i = 0; i + 1 < entries.Length; i += 2)
            {
                if (string.Equals(entries[i].ToString(), "total.allocated", StringComparison.OrdinalIgnoreCase) &&
                    long.TryParse(entries[i + 1].ToString(), out var totalAllocated))
                {
                    return totalAllocated;
                }
            }

            return null;
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
