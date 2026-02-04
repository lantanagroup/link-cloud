using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

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
}
