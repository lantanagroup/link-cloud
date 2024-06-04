using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Shared.Application.Services.SecretManager
{
    public class AzureKeyVaultSecretManager : ISecretManager
    {
        private readonly ILogger<AzureKeyVaultSecretManager> _logger;
        private readonly SecretClient _secretClient;

        public AzureKeyVaultSecretManager(ILogger<AzureKeyVaultSecretManager> logger, IOptions<SecretManagerSettings> secretMangerConfig)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
           _secretClient = new SecretClient(new Uri(secretMangerConfig.Value.ManagerUri), new DefaultAzureCredential());
        
            _logger.LogInformation("Azure Key Vault Secret Manager initialized");
        }

        public async Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken)
        {
            var secret = await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            return secret.Value.Value;
        }

        public async Task<string> GetSecretAsync(string secretName, string version, CancellationToken cancellationToken)
        {
            var secret = await _secretClient.GetSecretAsync(secretName, version, cancellationToken);
            return secret.Value.Value;
        }

        public async Task<bool> SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken)
        {
            var result = await _secretClient.SetSecretAsync(secretName, secretValue, cancellationToken);
            return result.Value != null;
        }
    }
}
