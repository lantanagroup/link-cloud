using LantanaGroup.Link.DMRP.Config;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace LantanaGroup.Link.DMRP.Api
{
    /// <summary>
    /// Supplies the bearer token the DMRP API's reporting-plan operations require.
    /// </summary>
    public interface IDmrpApiTokenProvider
    {
        /// <summary>
        /// A currently valid access token, fetched if there is not one already.
        /// </summary>
        /// <exception cref="DmrpApiException">The token could not be obtained.</exception>
        Task<string> GetAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Fetches a token with the client-credentials grant and holds it until it is close to expiring.
    /// </summary>
    /// <remarks>
    /// Cached because a single sync makes two calls and a token lasts an hour; fetching per call
    /// would mint two tokens to do one facility's work, and a facility-by-facility sync would mint
    /// one per request all the way through.
    /// <para>
    /// Registered as a singleton, so the cache is shared and the fetch is guarded by a semaphore
    /// rather than a lock - the work inside it is asynchronous. The token is re-checked after the
    /// wait: several callers can queue on the same expiry, and only the first of them needs to go
    /// and get one.
    /// </para>
    /// </remarks>
    public sealed class DmrpApiTokenProvider : IDmrpApiTokenProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<DmrpSettings> _settings;
        private readonly ILogger<DmrpApiTokenProvider> _logger;
        private readonly TimeProvider _timeProvider;
        private readonly SemaphoreSlim _gate = new(1, 1);

        private string? _token;
        private DateTimeOffset _expiresAt;

        public DmrpApiTokenProvider(IHttpClientFactory httpClientFactory, IOptions<DmrpSettings> settings,
            ILogger<DmrpApiTokenProvider> logger, TimeProvider timeProvider)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        public async Task<string> GetAsync(CancellationToken cancellationToken = default)
        {
            var api = _settings.Value.Api;

            if (!api.IsConfigured)
            {
                throw new DmrpApiException(
                    "The DMRP API is not configured. DMRP:Api needs BaseUrl, TokenUrl, ClientId and ClientSecret.");
            }

            if (TryUseCachedToken(out var cached))
            {
                return cached;
            }

            await _gate.WaitAsync(cancellationToken);

            try
            {
                // Another caller may have fetched one while this one waited.
                if (TryUseCachedToken(out cached))
                {
                    return cached;
                }

                var token = await FetchAsync(api, cancellationToken);

                _token = token.AccessToken;

                // Measured from now rather than from any timestamp in the token: the two clocks are
                // not the same one, and the margin is smaller than the skew between them could be.
                _expiresAt = _timeProvider.GetUtcNow()
                    .AddSeconds(Math.Max(0, token.ExpiresIn - api.TokenExpiryMarginSeconds));

                return _token!;
            }
            finally
            {
                _gate.Release();
            }
        }

        private bool TryUseCachedToken(out string token)
        {
            token = _token ?? string.Empty;

            return !string.IsNullOrEmpty(_token) && _timeProvider.GetUtcNow() < _expiresAt;
        }

        private async Task<DmrpTokenResponse> FetchAsync(DmrpApiSettings api, CancellationToken cancellationToken)
        {
            // JSON rather than the form encoding RFC 6749 describes for this grant. The token
            // endpoint this stands in front of takes a JSON body, and a client that posts a form
            // to it is answered 415 - so the wire format follows the service rather than the
            // specification.
            var request = new DmrpTokenRequest
            {
                GrantType = "client_credentials",
                ClientId = api.ClientId!,
                ClientSecret = api.ClientSecret!,
                Scope = string.IsNullOrWhiteSpace(api.Scope) ? null : api.Scope
            };

            using var client = _httpClientFactory.CreateClient(DmrpApiClient.HttpClientName);

            HttpResponseMessage response;

            try
            {
                response = await client.PostAsJsonAsync(api.TokenUrl, request, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new DmrpApiException("The DMRP API token endpoint could not be reached.", ex);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    // The status is logged and reported; the body is not. A token endpoint's failure
                    // body can echo the credential that was sent.
                    _logger.LogError("The DMRP API token endpoint answered {StatusCode}", (int)response.StatusCode);

                    throw new DmrpApiException(
                        $"The DMRP API token endpoint answered {(int)response.StatusCode}.");
                }

                var token = await response.Content.ReadFromJsonAsync<DmrpTokenResponse>(cancellationToken);

                if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
                {
                    throw new DmrpApiException("The DMRP API token endpoint returned no access token.");
                }

                return token;
            }
        }
    }
}
