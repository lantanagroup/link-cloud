using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using LantanaGroup.Link.Shared.Application.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security
{
    public class RefreshSigningKey : IRefreshSigningKey
    {
        private readonly ILogger<RefreshSigningKey> _logger;
        private readonly IDistributedCache _cache;
        private readonly ISecretManager _secretManager;
        private readonly IDataProtectionProvider _dataProtectionProvider;
        private readonly ILinkAdminMetrics _metrics;

        public RefreshSigningKey(ILogger<RefreshSigningKey> logger, IDistributedCache cache, ISecretManager secretManager, IDataProtectionProvider dataProtectionProvider, ILinkAdminMetrics metrics)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _secretManager = secretManager ?? throw new ArgumentNullException(nameof(secretManager));
            _dataProtectionProvider = dataProtectionProvider ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        }

        //TODO: Add back data protection once key persience is implemented
        public async Task<bool> ExecuteAsync(ClaimsPrincipal user)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivityWithTags("Refresh Link Bearer Service Signing Key",
            [
                new KeyValuePair<string, object?>(DiagnosticNames.UserId, user.Claims.First(c => c.Type == "sub").Value)
            ]);

            var key = GenerateRandomKey(64); // 64 bytes = 512 bits

            //update secret manager
            var result = await _secretManager.SetSecretAsync(LinkAdminConstants.LinkBearerService.LinkBearerKeyName, key, CancellationToken.None);

            if (!result)
            {
                _logger.LogLinkAdminTokenKeyRefreshException("Failed to update secret manager with new bearer key");
                return false;
            }

            _logger.LogLinkAdminTokenKeyRefreshed(DateTime.UtcNow);
            _metrics.IncrementTokenKeyRefreshCounter([]);

            var protector = _dataProtectionProvider.CreateProtector(LinkAdminConstants.LinkDataProtectors.LinkSigningKey);
            //_cache.SetString(LinkAdminConstants.LinkBearerService.LinkBearerKeyName, protector.Protect(key));
            _cache.SetString(LinkAdminConstants.LinkBearerService.LinkBearerKeyName, key);

            return true;
        }

        private static string GenerateRandomKey(int size)
        {
            using var rng = RandomNumberGenerator.Create();

            var randomNumber = new byte[size];
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }
}
