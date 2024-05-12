using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using Link.Authorization.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace LantanaGroup.Link.Shared.Application.Services.SecretManager
{
    internal class LocalSecretManager : ISecretManager
    {
        private readonly ILogger<LocalSecretManager> _logger;
        private Dictionary<string, string> _secrets = [];

        public LocalSecretManager(ILogger<LocalSecretManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _secrets.Add(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, GenerateRandomKey(64));
            _logger.LogInformation("Local Secret Manager initialized");
        }


        public Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken)
        {
            return Task.FromResult(GetSecret(secretName));
        }

        public Task<string> GetSecretAsync(string secretName, string version, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<bool> SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken)
        {
            return Task.FromResult(SetSecret(secretName, secretValue));
        }

        private string GetSecret(string secretKey)
        {
            return _secrets.TryGetValue(secretKey, out var secret) ? secret : null;
        }

        private bool SetSecret(string secretKey, string secretValue)
        {
            _secrets.TryGetValue(secretKey, out var secret);

            if (secret is not null)
            {
                _secrets[secretKey] = secretValue;
            }
            else
            {
                _secrets.Add(secretKey, secretValue);
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
