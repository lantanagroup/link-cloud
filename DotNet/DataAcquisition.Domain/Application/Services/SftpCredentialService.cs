using System.Text.Json;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

/// <summary>
/// Service for managing SFTP authentication credentials stored in a secure secret manager.
/// </summary>
public interface ISftpCredentialService
{
    /// <summary>
    /// Stores SFTP credentials in the secret manager for an organization.
    /// </summary>
    /// <param name="organizationId">The unique identifier for the organization.</param>
    /// <param name="credentials">The credentials to store.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><c>true</c> if credentials were stored successfully; otherwise, <c>false</c>.</returns>
    Task<bool> SetCredentialsAsync(string organizationId, SftpCredentialsModel credentials, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves SFTP credentials from the secret manager.
    /// </summary>
    /// <param name="organizationId">The unique identifier for the organization.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The credentials if found; otherwise, <c>null</c>.</returns>
    Task<SftpCredentialsModel?> GetCredentialsAsync(string organizationId, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes SFTP credentials from the secret manager.
    /// </summary>
    /// <param name="organizationId">The unique identifier for the organization.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><c>true</c> if credentials were deleted successfully; otherwise, <c>false</c>.</returns>
    Task<bool> DeleteCredentialsAsync(string organizationId, CancellationToken cancellationToken);

    /// <summary>
    /// Checks if credentials exist for an organization without retrieving the actual values.
    /// </summary>
    /// <param name="organizationId">The unique identifier for the organization.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A status model indicating whether credentials exist.</returns>
    Task<SftpCredentialStatusModel> GetCredentialStatusAsync(string organizationId, CancellationToken cancellationToken);
}

/// <summary>
/// Service for managing SFTP credentials using the configured secret manager.
/// </summary>
public class SftpCredentialService : ISftpCredentialService
{
    private readonly ISecretManager _secretManager;
    private readonly ILogger<SftpCredentialService> _logger;
    private const string SecretNamePrefix = "sftp-credentials-";

    public SftpCredentialService(
        ISecretManager secretManager,
        ILogger<SftpCredentialService> logger)
    {
        _secretManager = secretManager ?? throw new ArgumentNullException(nameof(secretManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates a consistent secret name for the given organization.
    /// </summary>
    private static string GetSecretName(string organizationId) => $"{SecretNamePrefix}{organizationId}";

    /// <inheritdoc/>
    public async Task<bool> SetCredentialsAsync(string organizationId, SftpCredentialsModel credentials, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
            throw new ArgumentNullException(nameof(organizationId));

        if (credentials == null)
            throw new ArgumentNullException(nameof(credentials));

        if (string.IsNullOrWhiteSpace(credentials.Username))
            throw new ArgumentException("Username cannot be empty", nameof(credentials));

        if (string.IsNullOrWhiteSpace(credentials.Password))
            throw new ArgumentException("Password cannot be empty", nameof(credentials));

        _logger.LogInformation("Setting SFTP credentials for organization {OrganizationId}", organizationId);

        var result = await _secretManager.SetSecretAsync(GetSecretName(organizationId), JsonSerializer.Serialize(credentials), cancellationToken);

        if (result)
        {
            _logger.LogInformation("Successfully stored SFTP credentials for organization {OrganizationId}", organizationId);
        }
        else
        {
            _logger.LogWarning("Failed to store SFTP credentials for organization {OrganizationId}", organizationId);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<SftpCredentialsModel?> GetCredentialsAsync(string organizationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
            throw new ArgumentNullException(nameof(organizationId));

        try
        {
            var value = await _secretManager.GetSecretAsync(GetSecretName(organizationId), cancellationToken);

            if (string.IsNullOrEmpty(value))
            {
                _logger.LogDebug("No SFTP credentials found for organization {OrganizationId}", organizationId.SanitizeForLog());
                return null;
            }

            return JsonSerializer.Deserialize<SftpCredentialsModel>(value);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize SFTP credentials for organization {OrganizationId}", organizationId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteCredentialsAsync(string organizationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
            throw new ArgumentNullException(nameof(organizationId));

        _logger.LogInformation("Deleting SFTP credentials for organization {OrganizationId}", organizationId.SanitizeAndRemove());

        var result = await _secretManager.DeleteSecretAsync(GetSecretName(organizationId), cancellationToken);

        if (result)
        {
            _logger.LogInformation("Successfully deleted SFTP credentials for organization {OrganizationId}", organizationId.SanitizeAndRemove());
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<SftpCredentialStatusModel> GetCredentialStatusAsync(string organizationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
            throw new ArgumentNullException(nameof(organizationId));

        try
        {
            var credentials = await GetCredentialsAsync(organizationId, cancellationToken);

            return new SftpCredentialStatusModel
            {
                HasCredentials = credentials != null &&
                                 !string.IsNullOrEmpty(credentials.Username) &&
                                 !string.IsNullOrEmpty(credentials.Password)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking credential status for organization {OrganizationId}", organizationId.SanitizeAndRemove());
            return new SftpCredentialStatusModel { HasCredentials = false };
        }
    }
}
