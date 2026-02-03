using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;

/// <summary>
/// Factory for creating SFTP sessions.
/// </summary>
public interface ISftpClientService
{
    /// <summary>
    /// Opens an SFTP session for the given configuration.
    /// The session maintains a single connection for all operations.
    /// Dispose the session when done to disconnect.
    /// </summary>
    Task<ISftpSession> OpenSessionAsync(
        SftpConfigurationModel sftpConfig,
        CancellationToken cancellationToken);
}
