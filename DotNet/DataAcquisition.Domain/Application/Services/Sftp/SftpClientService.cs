using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;

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

        var client = new SftpClient(
            sftpConfig.Host,
            sftpConfig.Port,
            credentials.Username,
            credentials.Password);

        client.ConnectionInfo.Timeout = sftpConfig.Timeout;
        await client.ConnectAsync(cancellationToken);

        logger.LogDebug(
            "Opened SFTP session to {Host}:{Port} for facility {FacilityId}",
            sftpConfig.Host, sftpConfig.Port, sftpConfig.OrganizationId);

        return new SftpSession(client, logger);
    }
}
