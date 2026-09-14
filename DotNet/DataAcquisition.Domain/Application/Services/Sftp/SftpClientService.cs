using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;

/// <summary>
/// Service for creating and managing SFTP connections.
/// Handles credential retrieval and session establishment for SFTP operations.
/// </summary>
public interface ISftpClientService
{
    /// <summary>
    /// Opens an SFTP session using the provided configuration.
    /// The session maintains a single connection for all subsequent file operations.
    /// Credentials are retrieved from the secure credential store based on the organization ID.
    /// </summary>
    /// <param name="sftpConfig">The SFTP configuration containing host, port, timeout, and organization identifier.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>An open <see cref="ISftpSession"/> ready for file operations.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no credentials are found for the organization.</exception>
    /// <remarks>
    /// The caller is responsible for disposing the returned session when finished.
    /// Use <c>await using</c> to ensure proper cleanup.
    /// </remarks>
    Task<ISftpSession> OpenSessionAsync(
        SftpConfigurationModel sftpConfig,
        CancellationToken cancellationToken);

    /// <summary>
    /// Opens an SFTP session using the provided host, port, and credentials.
    /// The session maintains a single connection for all subsequent file operations.
    /// </summary>
    /// <param name="host">Host name for the SFTP server.</param>
    /// <param name="port">Port number for the SFTP server.</param>
    /// <param name="credentials">Credentials for authenticating with the SFTP server.</param>
    /// <param name="timeout">Timeout for the SFTP session.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>An open <see cref="ISftpSession"/> ready for file operations.</returns>
    Task<ISftpSession> OpenSessionAsync(string host, int port, SftpCredentialsModel credentials, TimeSpan timeout, CancellationToken cancellationToken);
}

/// <summary>
/// Creates SFTP sessions using credentials from the secure credential store.
/// </summary>
public class SftpClientService(ILogger<SftpClientService> logger, ISftpCredentialService credentialService)
    : ISftpClientService
{
    /// <inheritdoc/>
    public async Task<ISftpSession> OpenSessionAsync(SftpConfigurationModel sftpConfig, CancellationToken cancellationToken)
    {
        var credentials = await credentialService.GetCredentialsAsync(
            sftpConfig.OrganizationId, cancellationToken);

        if (credentials is null || string.IsNullOrWhiteSpace(credentials.Username))
        {
            throw new InvalidOperationException($"No SFTP credentials found for facility {sftpConfig.OrganizationId}");
        }

        var session = await OpenSessionAsync(
            sftpConfig.Host,
            sftpConfig.Port,
            credentials,
            sftpConfig.Timeout,
            cancellationToken);

        logger.LogDebug(
            "Opened SFTP session to {Host}:{Port} for facility {FacilityId}",
            sftpConfig.Host.SanitizeForLog(), sftpConfig.Port.SanitizeForLog(), sftpConfig.OrganizationId.SanitizeForLog());

        return session;
    }

    /// <inheritdoc/>
    public async Task<ISftpSession> OpenSessionAsync(string host, int port, SftpCredentialsModel credentials, TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        var client = new SftpClient(host, port, credentials.Username, credentials.Password);
        client.ConnectionInfo.Timeout = timeout;

        try
        {
            await client.ConnectAsync(cancellationToken);
        }
        catch
        {
            // Nothing else owns the client until the session wraps it
            client.Dispose();
            throw;
        }

        return new SftpSession(client, logger);
    }
}
