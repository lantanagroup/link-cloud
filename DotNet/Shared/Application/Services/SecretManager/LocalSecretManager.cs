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
    /// </summary>
    /// <remarks>
    /// The file is per user, so every service running as that user on one machine shares it (the Aspire
    /// AppHost, or services run from an IDE). That is relied on: DataAcquisition stores a facility's
    /// credentials and its worker reads them back. So the file is treated as shared:
    /// <list type="bullet">
    /// <item>Each change re-reads the file, applies the change and writes it back while holding a lock file,
    /// so one process doesn't drop what another added since it last read.</item>
    /// <item>A read sees changes another process has written since (the file is reloaded when it changes).</item>
    /// <item>The file is written to a temporary file and then moved into place, so a reader never sees half
    /// of it.</item>
    /// <item>A file this process can't decrypt (written under another Data Protection key ring) is never
    /// written over: its secrets belong to whoever can read it. Changes are then kept in memory only, and
    /// <see cref="SetSecretAsync(string, string, CancellationToken)"/> returns false.</item>
    /// <item>A file another process holds open is retried for a short while before giving up.</item>
    /// </list>
    /// </remarks>
    public class LocalSecretManager : ISecretManager
    {
        private readonly ILogger<LocalSecretManager> _logger;
        private readonly IDataProtector _protector;
        private readonly string _secretsFilePath;
        private readonly string _lockFilePath;
        private readonly object _fileLock = new();
        private Dictionary<string, string> _values = [];

        /// <summary>The file's last write time and length when it was last read, to notice another process's writes.</summary>
        private (DateTime WrittenUtc, long Length)? _loadedVersion;

        /// <summary>True while the file exists but can't be decrypted by this process; it is then never written.</summary>
        private bool _fileUnreadable;

        private const string ProtectorPurpose = "LocalSecretManager.Secrets.v1";
        private const string SecretsFileName = "link-local-secrets.enc";
        private const string EnvironmentVariablePrefix = "LocalSecretManager__";
        private const int FileAttempts = 20;
        private static readonly TimeSpan FileRetryDelay = TimeSpan.FromMilliseconds(100);
        private readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = false };

        public LocalSecretManager(
            ILogger<LocalSecretManager> logger,
            IDataProtectionProvider dataProtectionProvider)
            : this(logger, dataProtectionProvider,
                // Store secrets file in user's local app data folder
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Link", "Secrets"))
        {
        }

        /// <summary>Uses <paramref name="secretsDirectory"/> for the file instead of the user's local app data folder.</summary>
        internal LocalSecretManager(
            ILogger<LocalSecretManager> logger,
            IDataProtectionProvider dataProtectionProvider,
            string secretsDirectory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _protector = dataProtectionProvider?.CreateProtector(ProtectorPurpose)
                ?? throw new ArgumentNullException(nameof(dataProtectionProvider));

            Directory.CreateDirectory(secretsDirectory);
            _secretsFilePath = Path.Combine(secretsDirectory, SecretsFileName);
            _lockFilePath = _secretsFilePath + ".lock";

            lock (_fileLock)
            {
                Reload(logLoaded: true);
            }

            // Ensure the bearer key exists (generate if not present)
            EnsureBearerKeyExists();

            _logger.LogInformation("Local Secret Manager initialized with persistence at {SecretsPath}", secretsDirectory);
        }

        public Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken)
        {
            string? secret;
            lock (_fileLock)
            {
                ReloadIfChanged();
                secret = _values.GetValueOrDefault(secretName);
            }

            secret ??= Environment.GetEnvironmentVariable(EnvironmentVariablePrefix + secretName);
            return Task.FromResult(secret);
        }

        public Task<string?> GetSecretAsync(string secretName, string version, CancellationToken cancellationToken)
        {
            // Version not supported in local secret manager
            return GetSecretAsync(secretName, cancellationToken);
        }

        public Task<bool> SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken)
        {
            var saved = Update(values => values[secretName] = secretValue);
            if (saved)
            {
                _logger.LogInformation("Secret {SecretName} saved to local secret manager", secretName.SanitizeAndRemove());
            }
            else
            {
                _logger.LogWarning("Secret {SecretName} is kept in memory only: the secrets file can't be read by this service, so it is not written over", secretName.SanitizeAndRemove());
            }
            return Task.FromResult(saved);
        }

        public Task<bool> DeleteSecretAsync(string secretName, CancellationToken cancellationToken)
        {
            var removed = false;
            Update(values => removed = values.Remove(secretName));
            _logger.LogInformation("Secret {SecretName} {Result} from local secret manager", secretName.SanitizeAndRemove(), removed ? "deleted" : "not found");
            return Task.FromResult(true);
        }

        /// <summary>
        /// Applies <paramref name="change"/> to the latest secrets and writes them, holding the lock file so no other
        /// process writes in between. Returns false, with the change applied in memory only, when the file can't be read.
        /// </summary>
        private bool Update(Action<Dictionary<string, string>> change)
        {
            lock (_fileLock)
            {
                using var crossProcessLock = AcquireLockFile();
                Reload(logLoaded: false);
                change(_values);

                if (_fileUnreadable)
                {
                    return false;
                }

                Write();
                return true;
            }
        }

        private void ReloadIfChanged()
        {
            var current = CurrentVersion();
            if (current != _loadedVersion)
            {
                Reload(logLoaded: false);
            }
        }

        /// <summary>Reads the file into memory. Call while holding <see cref="_fileLock"/>.</summary>
        private void Reload(bool logLoaded)
        {
            var version = CurrentVersion();
            if (version is null)
            {
                if (logLoaded)
                {
                    _logger.LogInformation("No existing secrets file found, starting with empty secrets");
                }
                _loadedVersion = null;
                _fileUnreadable = false;
                return;
            }

            string encryptedContent;
            try
            {
                encryptedContent = WithRetries(() => File.ReadAllText(_secretsFilePath));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Keep what is in memory; the next read or change tries again.
                _logger.LogWarning(ex, "Could not read the secrets file; keeping the secrets already loaded");
                return;
            }

            _loadedVersion = version;
            if (string.IsNullOrWhiteSpace(encryptedContent))
            {
                _fileUnreadable = false;
                return;
            }

            try
            {
                var decryptedContent = _protector.Unprotect(encryptedContent);
                _values = JsonSerializer.Deserialize<Dictionary<string, string>>(decryptedContent) ?? [];
                if (_fileUnreadable || logLoaded)
                {
                    _logger.LogInformation("Loaded {Count} secrets from encrypted file", _values.Count);
                }
                _fileUnreadable = false;
            }
            catch (Exception ex) when (ex is CryptographicException or JsonException)
            {
                if (!_fileUnreadable)
                {
                    _logger.LogWarning(ex, "Failed to decrypt secrets file (it was written under another Data Protection key ring, or the key has changed). " +
                        "It will not be written over; secrets set by this service are kept in memory only. Give every service sharing it the same DataProtection:KeyRing.");
                }
                _fileUnreadable = true;
            }
        }

        /// <summary>Writes the secrets to a temporary file, then moves it into place. Call while holding the lock file.</summary>
        private void Write()
        {
            var temporaryPath = $"{_secretsFilePath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
            try
            {
                var encryptedContent = _protector.Protect(JsonSerializer.Serialize(_values, _jsonSerializerOptions));
                File.WriteAllText(temporaryPath, encryptedContent);
                WithRetries(() => File.Move(temporaryPath, _secretsFilePath, overwrite: true));
                _loadedVersion = CurrentVersion();
                _logger.LogDebug("Secrets persisted to encrypted file");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save secrets to file");
                TryDelete(temporaryPath);
                throw;
            }
        }

        /// <summary>
        /// Opens the lock file exclusively, retrying while another process holds it, so changes from several processes
        /// are applied one at a time.
        /// </summary>
        private FileStream AcquireLockFile() =>
            WithRetries(() => new FileStream(_lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));

        private (DateTime WrittenUtc, long Length)? CurrentVersion()
        {
            var file = new FileInfo(_secretsFilePath);
            return file.Exists ? (file.LastWriteTimeUtc, file.Length) : null;
        }

        /// <summary>Runs <paramref name="action"/>, retrying for about two seconds while another process holds the file.</summary>
        private static T WithRetries<T>(Func<T> action)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return action();
                }
                catch (IOException) when (attempt < FileAttempts)
                {
                    Thread.Sleep(FileRetryDelay);
                }
                catch (UnauthorizedAccessException) when (attempt < FileAttempts)
                {
                    // Windows reports a file being replaced as access denied for a moment.
                    Thread.Sleep(FileRetryDelay);
                }
            }
        }

        private static void WithRetries(Action action) => WithRetries(() => { action(); return true; });

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception)
            {
                // Best effort: a stray temporary file holds nothing the real file doesn't.
            }
        }

        private void EnsureBearerKeyExists()
        {
            const string bearerKeyName = LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName;

            lock (_fileLock)
            {
                if (_values.ContainsKey(bearerKeyName)) return;
            }

            // Another process may have added it since this one read the file; Update re-reads before changing.
            Update(values => values.TryAdd(bearerKeyName, GenerateRandomKey(64)));
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
