using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.SecretManagers
{
    public class LinkAzureKeyVault : ISecretManager
    {
        private readonly ILogger<LinkAzureKeyVault> _logger;
        private readonly SecretClient _secretClient;
    
        public LinkAzureKeyVault(ILogger<LinkAzureKeyVault> logger, IOptions<SecretManagerConfig> secretMangerConfig)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _secretClient = new SecretClient(new Uri(secretMangerConfig.Value.ManagerUri), new DefaultAzureCredential());
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
