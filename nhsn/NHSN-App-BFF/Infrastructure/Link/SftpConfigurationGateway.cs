using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;
using LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link.Mappers;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

// ISftpConfigurationGateway over LinkSdk's IDataAcquisitionServiceClient. Credentials are a
// separate write-only resource on Data Acquisition's side and are never read back — only whether
// they exist.
internal sealed class SftpConfigurationGateway : ISftpConfigurationGateway
{
    private const string ServiceName = "DataAcquisition";

    private readonly IDataAcquisitionServiceClient _dataAcquisitionClient;
    private readonly ILogger<SftpConfigurationGateway> _logger;

    public SftpConfigurationGateway(IDataAcquisitionServiceClient dataAcquisitionClient, ILogger<SftpConfigurationGateway> logger)
    {
        _dataAcquisitionClient = dataAcquisitionClient;
        _logger = logger;
    }

    public async Task<SftpConfig?> GetConfigurationAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var wire = await FetchAsync(facilityId, cancellationToken);
        return SftpConfigurationMapper.ToDomain(wire);
    }

    public async Task SaveConfigurationAsync(string facilityId, SftpConfig config, CancellationToken cancellationToken = default)
    {
        var existing = await FetchAsync(facilityId, cancellationToken);
        var payload = SftpConfigurationMapper.ToPayload(config, existing?.Id);

        if (existing is null)
        {
            var createResponse = await _dataAcquisitionClient.CreateSftpConfigurationAsync(facilityId, payload, cancellationToken);
            LogIfFailed(createResponse, nameof(SaveConfigurationAsync));
            LinkResponseHandler.EnsureSuccess(createResponse, ServiceName, nameof(SaveConfigurationAsync));
            _logger.LogInformation("Created sFTP configuration for facility {FacilityId}.", facilityId);
            return;
        }

        var updateResponse = await _dataAcquisitionClient.UpdateSftpConfigurationAsync(
            facilityId, existing.Id ?? string.Empty, payload, cancellationToken);
        LogIfFailed(updateResponse, nameof(SaveConfigurationAsync));
        LinkResponseHandler.EnsureSuccess(updateResponse, ServiceName, nameof(SaveConfigurationAsync));
        _logger.LogInformation("Updated sFTP configuration for facility {FacilityId}.", facilityId);
    }

    // LinkServiceException's own Message deliberately excludes RawBody, so the actual validation
    // detail Data Acquisition sent back would otherwise never surface anywhere.
    private void LogIfFailed(LinkApiResponse response, string operation)
    {
        if (response.StatusCode is < 200 or >= 300)
        {
            _logger.LogWarning(
                "DataAcquisition.{Operation} failed with status {StatusCode}: {RawBody}",
                operation, response.StatusCode, response.RawBody);
        }
    }

    public async Task SaveCredentialsAsync(string facilityId, string username, string password, CancellationToken cancellationToken = default)
    {
        var response = await _dataAcquisitionClient.UpdateSftpCredentialsAsync(
            facilityId, new SftpCredentialsPayload { Username = username, Password = password }, cancellationToken);
        LinkResponseHandler.EnsureSuccess(response, ServiceName, nameof(SaveCredentialsAsync));
    }

    public async Task<bool> GetHasCredentialsAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var response = await _dataAcquisitionClient.GetSftpCredentialStatusAsync(facilityId, cancellationToken);
        var wire = LinkResponseHandler.OptionalFromRawBody<SftpCredentialStatusWire>(response, ServiceName, nameof(GetHasCredentialsAsync));
        return wire?.HasCredentials ?? false;
    }

    private async Task<SftpConfigurationWire?> FetchAsync(string facilityId, CancellationToken cancellationToken)
    {
        var response = await _dataAcquisitionClient.GetOrganizationSftpConfigurationAsync(facilityId, cancellationToken);
        return LinkResponseHandler.OptionalFromRawBody<SftpConfigurationWire>(response, ServiceName, nameof(GetConfigurationAsync));
    }
}
