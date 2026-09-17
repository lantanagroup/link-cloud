using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using Link.Authorization.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text.Json;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.Shared.Application.Services.SecretManager
{
    /// <summary>
    /// Local secret manager for development environments.
    /// Stores secrets in an encrypted JSON file using Data Protection API.
    /// Falls back to environment variables for secrets not found in the JSON file.
    /// Secrets are loaded into memory on startup for fast access.
    /// </summary>
    public class LocalSecretManager : ISecretManager
    {
        private readonly ILogger<LocalSecretManager> _logger;
        private readonly IDataProtector _protector;
        private readonly string _secretsFilePath;
        private readonly object _fileLock = new();
        private Dictionary<string, string> _values = [];

        private const string ProtectorPurpose = "LocalSecretManager.Secrets.v1";
        private const string SecretsFileName = "link-local-secrets.enc";
        private const string EnvironmentVariablePrefix = "LocalSecretManager__";
        private readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = false };

        public LocalSecretManager(
            ILogger<LocalSecretManager> logger,
            IDataProtectionProvider dataProtectionProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _protector = dataProtectionProvider?.CreateProtector(ProtectorPurpose)
                ?? throw new ArgumentNullException(nameof(dataProtectionProvider));

            // Store secrets file in user's local app data folder
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var managerPath = Path.Combine(appDataPath, "Link", "Secrets");
            Directory.CreateDirectory(managerPath);
            _secretsFilePath = Path.Combine(managerPath, SecretsFileName);

            // Load existing secrets from file
            LoadSecretsFromFile();

            // Ensure the bearer key exists (generate if not present)
            EnsureBearerKeyExists();

            _logger.LogInformation("Local Secret Manager initialized with persistence at {SecretsPath}", managerPath);
        }

        public Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken)
        {
            var secret = _values.GetValueOrDefault(secretName);
            if (secret == null)
            {
                secret = Environment.GetEnvironmentVariable(EnvironmentVariablePrefix + secretName);
            }
            return Task.FromResult(secret);
        }

        public Task<string?> GetSecretAsync(string secretName, string version, CancellationToken cancellationToken)
        {
            // Version not supported in local secret manager
            return GetSecretAsync(secretName, cancellationToken);
        }

        public Task<bool> SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken)
        {
            _values[secretName] = secretValue;
            SaveSecretsToFile();
            _logger.LogInformation("Secret {SecretName} saved to local secret manager", secretName.SanitizeAndRemove());
            return Task.FromResult(true);
        }

        public Task<bool> DeleteSecretAsync(string secretName, CancellationToken cancellationToken)
        {
            var removed = _values.Remove(secretName);
            if (removed)
            {
                SaveSecretsToFile();
            }
            _logger.LogInformation("Secret {SecretName} {Result} from local secret manager", secretName.SanitizeAndRemove(), removed ? "deleted" : "not found");
            return Task.FromResult(true);
        }

        private void LoadSecretsFromFile()
        {
            lock (_fileLock)
            {
                try
                {
                    if (!File.Exists(_secretsFilePath))
                    {
                        _logger.LogInformation("No existing secrets file found, starting with empty secrets");
                        return;
                    }

                    var encryptedContent = File.ReadAllText(_secretsFilePath);
                    if (string.IsNullOrWhiteSpace(encryptedContent))
                    {
                        return;
                    }

                    var decryptedContent = _protector.Unprotect(encryptedContent);
                    _values = JsonSerializer.Deserialize<Dictionary<string, string>>(decryptedContent) ?? [];

                    _logger.LogInformation("Loaded {Count} secrets from encrypted file", _values.Count);
                }
                catch (CryptographicException ex)
                {
                    _logger.LogWarning(ex, "Failed to decrypt secrets file (encryption key may have changed). Starting with empty secrets.");
                    _values = [];
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading secrets from file. Starting with empty secrets.");
                    _values = [];
                }
            }
        }

        private void SaveSecretsToFile()
        {
            lock (_fileLock)
            {
                try
                {
                    var encryptedContent = _protector.Protect(JsonSerializer.Serialize(_values, _jsonSerializerOptions));
                    File.WriteAllText(_secretsFilePath, encryptedContent);
                    _logger.LogDebug("Secrets persisted to encrypted file");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save secrets to file");
                    throw;
                }
            }
        }

        private void EnsureBearerKeyExists()
        {
            const string bearerKeyName = LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName;

            if (_values.ContainsKey(bearerKeyName)) return;

            var key = GenerateRandomKey(64);
            _values[bearerKeyName] = key;

            SaveSecretsToFile();
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
