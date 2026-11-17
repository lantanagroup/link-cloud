using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Medallion.Threading;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Auth;

/// <summary>
/// Caches each facility's EHR access token and takes a per-facility lock to mint or discard it, so concurrent
/// cache misses share one mint and a late 401 cannot discard a token minted after the failing request began.
/// </summary>
public class FacilityAccessTokenCache
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(30);

    private readonly ICacheService _cacheService;
    private readonly IDistributedSemaphoreProvider _semaphoreProvider;
    private readonly ILogger<FacilityAccessTokenCache> _logger;

    public FacilityAccessTokenCache(ICacheService cacheService,
                                    IDistributedSemaphoreProvider semaphoreProvider,
                                    ILogger<FacilityAccessTokenCache> logger)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _semaphoreProvider = semaphoreProvider ?? throw new ArgumentNullException(nameof(semaphoreProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Returns the facility's cached token, or mints one with <paramref name="mint"/> when none is cached.
    /// Returns null when minting fails.
    /// </summary>
    public async Task<string?> GetOrMintAsync(string facilityId,
                                              Func<CancellationToken, Task<(string Token, int ExpiresInSeconds)?>> mint,
                                              CancellationToken cancellationToken = default)
    {
        var cachedToken = await _cacheService.GetAsync<string>(facilityId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cachedToken))
        {
            return cachedToken;
        }

        await using var handle = await TryLockAsync(facilityId, cancellationToken);
        if (handle != null)
        {
            // Another request may have minted a token while this one waited for the lock.
            cachedToken = await _cacheService.GetAsync<string>(facilityId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(cachedToken))
            {
                return cachedToken;
            }
        }

        var minted = await mint(cancellationToken);
        if (minted is not { } token)
        {
            return null;
        }

        var lifetime = TimeSpan.FromSeconds(token.ExpiresInSeconds);

        // Written before the token, so an invalidation never pairs the new token with an older minted-at time.
        await _cacheService.SetAsync(MintedAtKey(facilityId),
                                     DateTimeOffset.UtcNow,
                                     lifetime,
                                     ExpirationType.Absolute,
                                     cancellationToken);
        await _cacheService.SetAsync(facilityId, token.Token, lifetime, ExpirationType.Absolute, cancellationToken);

        return token.Token;
    }

    /// <summary>
    /// Discards the facility's cached token after a 401, unless it was minted at or after
    /// <paramref name="requestStartedUtc"/> and so cannot be the token the failing request was refused with.
    /// </summary>
    public async Task InvalidateAsync(string facilityId,
                                      DateTimeOffset requestStartedUtc,
                                      CancellationToken cancellationToken = default)
    {
        await using var handle = await TryLockAsync(facilityId, cancellationToken);
        if (handle == null)
        {
            // Without the lock this check could interleave with a mint, so keep the token and let it expire.
            _logger.LogWarning("Kept the cached access token for facility {FacilityId} after a 401: the token lock was unavailable.",
                               facilityId.SanitizeForLog());
            return;
        }

        var mintedAtUtc = await _cacheService.GetAsync<DateTimeOffset>(MintedAtKey(facilityId), cancellationToken);
        if (mintedAtUtc >= requestStartedUtc)
        {
            return;
        }

        await _cacheService.RemoveAsync(facilityId, cancellationToken);
    }

    private static string MintedAtKey(string facilityId) => $"{facilityId}:access-token-minted-at";

    private async Task<IDistributedSynchronizationHandle?> TryLockAsync(string facilityId,
                                                                        CancellationToken cancellationToken)
    {
        try
        {
            var semaphore = _semaphoreProvider.CreateSemaphore($"da:access-token:{facilityId}", maxCount: 1);
            return await semaphore.TryAcquireAsync(LockTimeout, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                               "Could not acquire the access token lock for facility {FacilityId}.",
                               facilityId.SanitizeForLog());
            return null;
        }
    }
}
