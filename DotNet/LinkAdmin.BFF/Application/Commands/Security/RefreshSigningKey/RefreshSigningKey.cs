using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using LantanaGroup.Link.Shared.Application.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Link.Authorization.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security
{
    public class RefreshSigningKey : IRefreshSigningKey
    {
        private readonly ILogger<RefreshSigningKey> _logger;
        private readonly ISecretManager _secretManager;
        private readonly IOptions<DataProtectionSettings> _dataProtectionSettings;
        private readonly IDataProtectionProvider _dataProtectionProvider;
        private readonly ILinkAdminMetrics _metrics;
        private readonly IOptions<CacheSettings> _cacheSettings;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public RefreshSigningKey(ILogger<RefreshSigningKey> logger, ISecretManager secretManager, IOptions<DataProtectionSettings> dataProtectionSettings, IDataProtectionProvider dataProtectionProvider, ILinkAdminMetrics metrics, IOptions<CacheSettings> cacheSettings, IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _secretManager = secretManager ?? throw new ArgumentNullException(nameof(secretManager));
            _dataProtectionSettings = dataProtectionSettings ?? throw new ArgumentNullException(nameof(dataProtectionSettings));
            _dataProtectionProvider = dataProtectionProvider ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _cacheSettings = cacheSettings ?? throw new ArgumentNullException(nameof(cacheSettings));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));            
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
            var result = await _secretManager.SetSecretAsync(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, key, CancellationToken.None);

            if (!result)
            {
                _logger.LogLinkAdminTokenKeyRefreshException("Failed to update secret manager with new bearer key");
                return false;
            }

            _logger.LogLinkAdminTokenKeyRefreshed(DateTime.UtcNow);
            _metrics.IncrementTokenKeyRefreshCounter([]);

            if(_cacheSettings.Value.Enabled)
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var _cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

                if (_dataProtectionSettings.Value.Enabled)
                {
                    var protector = _dataProtectionProvider.CreateProtector(LinkAdminConstants.LinkDataProtectors.LinkSigningKey);
                    _cache.SetString(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, protector.Protect(key));
                }
                else
                {
                    _cache.SetString(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, key);
                }              
                
            }            

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
