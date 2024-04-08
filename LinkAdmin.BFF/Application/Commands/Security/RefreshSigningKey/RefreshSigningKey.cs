
using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Cryptography;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security
{
    public class RefreshSigningKey : IRefreshSigningKey
    {
        private readonly ILogger<RefreshSigningKey> _logger;
        private readonly IDistributedCache _cache;
        private readonly ISecretManager _secretManager;
        private readonly IDataProtectionProvider _dataProtectionProvider;

        public RefreshSigningKey(ILogger<RefreshSigningKey> logger, IDistributedCache cache, ISecretManager secretManager, IDataProtectionProvider dataProtectionProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _secretManager = secretManager ?? throw new ArgumentNullException(nameof(secretManager));
            _dataProtectionProvider = dataProtectionProvider ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
        }

        public async Task<bool> ExecuteAsync()
        {
            var key = GenerateRandomKey(64); // 64 bytes = 512 bits

            //update secret manager
            var result = await _secretManager.SetSecretAsync(LinkAdminConstants.LinkBearerService.LinkBearerKeyName, key, CancellationToken.None);

            if (!result)
            {
                _logger.LogError("Failed to update secret manager with new bearer key");
                return false;
            }

            var protector = _dataProtectionProvider.CreateProtector(LinkAdminConstants.LinkDataProtectors.LinkSigningKey);
            _cache.SetString(LinkAdminConstants.LinkBearerService.LinkBearerKeyName, protector.Protect(key));

            _logger.LogInformation("Bearer key refreshed");

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
