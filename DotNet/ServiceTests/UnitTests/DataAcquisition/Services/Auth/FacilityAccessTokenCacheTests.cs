using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Auth;
using LantanaGroup.Link.Shared.Application.Extensions.Caching;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Medallion.Threading;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Auth;

[Trait("Category", "UnitTests")]
public class FacilityAccessTokenCacheTests
{
    private const string FacilityId = "test-facility";
    private const string MintedAtKey = "test-facility:access-token-minted-at";

    private static FacilityAccessTokenCache BuildCache(ICacheService cacheService, IDistributedSemaphoreProvider locks)
    {
        return new FacilityAccessTokenCache(cacheService, locks, Mock.Of<ILogger<FacilityAccessTokenCache>>());
    }

    private static ICacheService InMemory()
    {
        return new InMemoryCacheService(new MemoryCache(new MemoryCacheOptions()));
    }

    private static Func<CancellationToken, Task<(string Token, int ExpiresInSeconds)?>> Mints(string token,
                                                                                               Action? onMint = null)
    {
        return _ =>
        {
            onMint?.Invoke();
            return Task.FromResult<(string Token, int ExpiresInSeconds)?>((token, 300));
        };
    }

    [Fact]
    public async Task GetOrMintAsync_CachedToken_ReturnsItWithoutMinting()
    {
        var cacheService = InMemory();
        await cacheService.SetAsync(FacilityId, "cached-token", TimeSpan.FromMinutes(5), ExpirationType.Absolute);
        var mints = 0;

        var token = await BuildCache(cacheService, TokenLockProviders.Granting())
            .GetOrMintAsync(FacilityId, Mints("new-token", () => mints++));

        Assert.Equal("cached-token", token);
        Assert.Equal(0, mints);
    }

    [Fact]
    public async Task GetOrMintAsync_NoCachedToken_CachesTheTokenAndWhenItWasMinted()
    {
        var cacheService = InMemory();
        var before = DateTimeOffset.UtcNow;

        var token = await BuildCache(cacheService, TokenLockProviders.Granting())
            .GetOrMintAsync(FacilityId, Mints("new-token"));

        Assert.Equal("new-token", token);
        Assert.Equal("new-token", await cacheService.GetAsync<string>(FacilityId));
        var mintedAt = await cacheService.GetAsync<DateTimeOffset>(MintedAtKey);
        Assert.InRange(mintedAt, before, DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task GetOrMintAsync_TokenMintedWhileWaitingForTheLock_UsesItWithoutMinting()
    {
        var cacheService = new Mock<ICacheService>();
        cacheService
            .SetupSequence(c => c.GetAsync<string>(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string)null!)
            .ReturnsAsync("minted-by-another-request");
        var mints = 0;

        var token = await BuildCache(cacheService.Object, TokenLockProviders.Granting())
            .GetOrMintAsync(FacilityId, Mints("new-token", () => mints++));

        Assert.Equal("minted-by-another-request", token);
        Assert.Equal(0, mints);
    }

    [Fact]
    public async Task GetOrMintAsync_ConcurrentMisses_MintOnce()
    {
        var cacheService = InMemory();
        var cache = BuildCache(cacheService, TokenLockProviders.Serialized());
        var mints = 0;

        async Task<(string Token, int ExpiresInSeconds)?> SlowMint(CancellationToken ct)
        {
            Interlocked.Increment(ref mints);
            await Task.Delay(50, ct);
            return ("new-token", 300);
        }

        var tokens = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => cache.GetOrMintAsync(FacilityId, SlowMint)));

        Assert.Equal(1, mints);
        Assert.All(tokens, t => Assert.Equal("new-token", t));
    }

    [Fact]
    public async Task GetOrMintAsync_MintFails_CachesNothing()
    {
        var cacheService = InMemory();

        var token = await BuildCache(cacheService, TokenLockProviders.Granting())
            .GetOrMintAsync(FacilityId, _ => Task.FromResult<(string Token, int ExpiresInSeconds)?>(null));

        Assert.Null(token);
        Assert.Null(await cacheService.GetAsync<string>(FacilityId));
    }

    [Fact]
    public async Task GetOrMintAsync_LockUnavailable_StillMints()
    {
        var cacheService = InMemory();

        var token = await BuildCache(cacheService, TokenLockProviders.Unavailable())
            .GetOrMintAsync(FacilityId, Mints("new-token"));

        Assert.Equal("new-token", token);
        Assert.Equal("new-token", await cacheService.GetAsync<string>(FacilityId));
    }

    [Fact]
    public async Task InvalidateAsync_TokenMintedBeforeTheRequestStarted_RemovesIt()
    {
        var cacheService = InMemory();
        var cache = BuildCache(cacheService, TokenLockProviders.Granting());
        await cache.GetOrMintAsync(FacilityId, Mints("old-token"));
        await Task.Delay(20);

        await cache.InvalidateAsync(FacilityId, DateTimeOffset.UtcNow);

        Assert.Null(await cacheService.GetAsync<string>(FacilityId));
    }

    [Fact]
    public async Task InvalidateAsync_TokenMintedAfterTheRequestStarted_KeepsIt()
    {
        var cacheService = InMemory();
        var cache = BuildCache(cacheService, TokenLockProviders.Granting());
        var requestStartedUtc = DateTimeOffset.UtcNow;
        await Task.Delay(20);
        await cache.GetOrMintAsync(FacilityId, Mints("fresh-token"));

        await cache.InvalidateAsync(FacilityId, requestStartedUtc);

        Assert.Equal("fresh-token", await cacheService.GetAsync<string>(FacilityId));
    }

    [Fact]
    public async Task InvalidateAsync_NoMintTimeRecorded_RemovesTheToken()
    {
        // A token cached before minted-at times were recorded has no time beside it.
        var cacheService = InMemory();
        await cacheService.SetAsync(FacilityId, "legacy-token", TimeSpan.FromMinutes(5), ExpirationType.Absolute);

        await BuildCache(cacheService, TokenLockProviders.Granting()).InvalidateAsync(FacilityId, DateTimeOffset.UtcNow);

        Assert.Null(await cacheService.GetAsync<string>(FacilityId));
    }

    [Fact]
    public async Task InvalidateAsync_LockUnavailable_KeepsTheToken()
    {
        var cacheService = InMemory();
        await cacheService.SetAsync(FacilityId, "old-token", TimeSpan.FromMinutes(5), ExpirationType.Absolute);

        await BuildCache(cacheService, TokenLockProviders.Unavailable()).InvalidateAsync(FacilityId, DateTimeOffset.UtcNow);

        Assert.Equal("old-token", await cacheService.GetAsync<string>(FacilityId));
    }

    [Fact]
    public async Task InvalidateAsync_LateUnauthorizedForAnOlderToken_KeepsTheTokenMintedSince()
    {
        // Requests A and B both send T1. A's 401 evicts T1 and its retry mints T2. B's 401 arrives late.
        var cacheService = InMemory();
        var cache = BuildCache(cacheService, TokenLockProviders.Granting());
        await cache.GetOrMintAsync(FacilityId, Mints("T1"));
        await Task.Delay(20);
        var requestAStartedUtc = DateTimeOffset.UtcNow;
        var requestBStartedUtc = DateTimeOffset.UtcNow;
        await Task.Delay(20);

        await cache.InvalidateAsync(FacilityId, requestAStartedUtc);
        await cache.GetOrMintAsync(FacilityId, Mints("T2"));
        await cache.InvalidateAsync(FacilityId, requestBStartedUtc);

        Assert.Equal("T2", await cacheService.GetAsync<string>(FacilityId));
    }
}
