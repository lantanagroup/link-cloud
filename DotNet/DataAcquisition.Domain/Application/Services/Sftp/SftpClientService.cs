using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;

public class SftpClientService : ISftpClientService
{
    private readonly ILogger<SftpClientService> _logger;
    private readonly ISftpCredentialService _credentialService;

    public SftpClientService(
        ILogger<SftpClientService> logger,
        ISftpCredentialService credentialService)
    {
        _logger = logger;
        _credentialService = credentialService;
    }

    public async Task<ISftpSession> OpenSessionAsync(
        SftpConfiguration sftpConfig,
        CancellationToken cancellationToken)
    {
        var credentials = await _credentialService.GetCredentialsAsync(
            sftpConfig.OrganizationId, cancellationToken);

        if (credentials is null || string.IsNullOrWhiteSpace(credentials.Username))
        {
            throw new InvalidOperationException(
                $"No SFTP credentials found for facility {sftpConfig.OrganizationId}");
        }

        var client = new SftpClient(
            sftpConfig.Host,
            sftpConfig.Port,
            credentials.Username,
            credentials.Password);

        client.ConnectionInfo.Timeout = sftpConfig.Timeout;
        await client.ConnectAsync(cancellationToken);

        _logger.LogDebug(
            "Opened SFTP session to {Host}:{Port} for facility {FacilityId}",
            sftpConfig.Host, sftpConfig.Port, sftpConfig.OrganizationId);

        return new SftpSession(client, _logger);
    }
}
