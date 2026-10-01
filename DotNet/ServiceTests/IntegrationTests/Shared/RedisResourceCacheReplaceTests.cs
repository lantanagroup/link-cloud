using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.ResourceCache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;
using RedisExtensionsConfiguration = StackExchange.Redis.Extensions.Core.Configuration.RedisConfiguration;
using StackExchange.Redis.Extensions.Core.Implementations;
using StackExchange.Redis.Extensions.System.Text.Json;
using Testcontainers.Redis;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Shared;

/// <summary>
/// <see cref="RedisResourceCache.ReplaceResourcesAsync"/> against a real Redis.
/// </summary>
/// <remarks>
/// The unit tests mock <see cref="ITransaction"/>, so they can prove the commands are queued on a
/// transaction rather than issued directly, and nothing more. Whether Redis accepts the transaction
/// and whether the entry afterwards holds exactly what was asked for are only answerable against a
/// real server, and this is the operation the non-org encounter strip depends on to remove anything
/// at all.
/// <para>
/// What these do <i>not</i> cover: atomicity. Every assertion here passes against a sequential
/// delete-then-write, because that reaches the same final state and a single-threaded test cannot
/// observe the window in between where the key is empty. The mocked unit test is what holds the
/// implementation to a transaction; this holds the transaction to producing the right entry. Neither
/// is sufficient alone, so do not delete one on the strength of the other.
/// </para>
/// </remarks>
[Trait("Category", "IntegrationTests")]
public class RedisResourceCacheReplaceTests : IAsyncLifetime
{
    private const int CacheEntryTtlDays = 7;

    private readonly RedisContainer _redis = new RedisBuilder().WithImage("redis:latest").Build();

    private RedisConnectionPoolManager _pool = null!;
    private IRedisDatabase _database = null!;
    private RedisResourceCache _cache = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();

        var configuration = new RedisExtensionsConfiguration { ConnectionString = _redis.GetConnectionString() };
        _pool = new RedisConnectionPoolManager(configuration);
        _database = new RedisDatabase(
            _pool,
            new SystemTextJsonSerializer(),
            configuration.ServerEnumerationStrategy,
            configuration.Database,
            configuration.MaxValueLength);

        _cache = new RedisResourceCache(
            _database,
            Options.Create(new ResourceCacheSettings
            {
                Redis = new ResourceCacheRedisSettings { CacheEntryTtlDays = CacheEntryTtlDays }
            }),
            Mock.Of<ILogger<RedisResourceCache>>());
    }

    public async Task DisposeAsync()
    {
        _pool?.Dispose();
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task ReplaceResourcesAsync_LeavesOnlyTheResourcesGiven()
    {
        const string cacheKey = "corr-1:Encounter";

        await _cache.AppendResourcesAsync(
            cacheKey,
            [Encounter("enc-org"), Encounter("enc-nonorg-1"), Encounter("enc-nonorg-2")],
            ResourceType.Encounter);

        Assert.Equal(3, await _cache.GetResourceCountAsync(cacheKey));

        // What the strip does: keep the org encounter, drop the rest.
        await _cache.ReplaceResourcesAsync(cacheKey, [Encounter("enc-org")], ResourceType.Encounter);

        var remaining = await _cache.GetAsync(cacheKey);

        // An append could never have produced this: the two non-org encounters are gone, which is the
        // whole point of having a replace.
        Assert.Single(remaining);
        Assert.Equal("enc-org", remaining[0].Id);
        Assert.Equal(1, await _cache.GetResourceCountAsync(cacheKey));
    }

    [Fact]
    public async Task ReplaceResourcesAsync_KeepsTheEntryExpiring()
    {
        const string cacheKey = "corr-2:Encounter";

        await _cache.AppendResourcesAsync(cacheKey, [Encounter("enc-a")], ResourceType.Encounter);
        await _cache.ReplaceResourcesAsync(cacheKey, [Encounter("enc-b")], ResourceType.Encounter);

        // The transaction deletes the key, so the lifetime has to be reapplied with the new contents.
        // Without that the replaced entry would never expire and the 7-day bound would be gone.
        var ttl = await _database.Database.KeyTimeToLiveAsync(cacheKey);

        Assert.NotNull(ttl);
        Assert.InRange(ttl!.Value, TimeSpan.FromDays(CacheEntryTtlDays - 1), TimeSpan.FromDays(CacheEntryTtlDays));
    }

    [Fact]
    public async Task ReplaceResourcesAsync_WithNoResources_RemovesTheEntry()
    {
        const string cacheKey = "corr-3:Encounter";

        await _cache.AppendResourcesAsync(cacheKey, [Encounter("enc-a")], ResourceType.Encounter);

        // Every encounter stripped. A transaction that wrote no fields would leave the key behind
        // empty, and an empty entry reads differently from an absent one: absent falls through to
        // durable storage, empty is served as "this correlation has no encounters".
        await _cache.ReplaceResourcesAsync(cacheKey, [], ResourceType.Encounter);

        Assert.False(await _database.Database.KeyExistsAsync(cacheKey));
        Assert.False(await _cache.HasResourcesAsync(cacheKey));
    }

    [Fact]
    public async Task ReplaceResourcesAsync_DoesNotDisturbTheCorrelationsOtherKeys()
    {
        const string encounterKey = "corr-4:Encounter";
        const string observationKey = "corr-4:Observation";

        await _cache.AppendResourcesAsync(encounterKey, [Encounter("enc-a")], ResourceType.Encounter);
        await _cache.AppendResourcesAsync(observationKey, [Observation("obs-a")], ResourceType.Observation);

        await _cache.ReplaceResourcesAsync(encounterKey, [], ResourceType.Encounter);

        // The strip targets one resource type. Reaching wider would discard resources nothing asked
        // it to touch.
        Assert.Single(await _cache.GetAsync(observationKey));
    }

    [Fact]
    public async Task ReplaceResourcesAsync_PreservesTheDurableResourceCountField()
    {
        const string cacheKey = "corr-5:Encounter";

        await _cache.AppendResourcesAsync(cacheKey, [Encounter("enc-a"), Encounter("enc-b")], ResourceType.Encounter);
        await _cache.SetDurableResourceCountAsync(cacheKey, 2);

        await _cache.ReplaceResourcesAsync(cacheKey, [Encounter("enc-a")], ResourceType.Encounter);

        // The replace clears the whole hash, metadata included, so the count it carried is gone. A
        // reader treats a missing count as unknown and trusts the entry, rather than comparing
        // against the pre-replace figure and calling the entry partial. The caller that needs the
        // count to survive records it again; HybridResourceCache does exactly that.
        Assert.Null(await _cache.GetDurableResourceCountAsync(cacheKey));
        Assert.Equal(1, await _cache.GetResourceCountAsync(cacheKey));
    }

    private static Encounter Encounter(string id) => new() { Id = id };

    private static Observation Observation(string id) => new() { Id = id };
}
