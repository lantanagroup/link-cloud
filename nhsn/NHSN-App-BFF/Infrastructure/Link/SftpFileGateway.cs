using System.Text.Json;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;
using LantanaGroup.Link.Sdk.Clients;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

// ISftpFileGateway over LinkSdk's IDataAcquisitionServiceClient.TestSftpConnectionAsync, which
// tests the facility's already-saved configuration and credentials.
internal sealed class SftpFileGateway : ISftpFileGateway
{
    private const string ServiceName = "DataAcquisition";

    private static readonly JsonSerializerOptions RawBodyOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IDataAcquisitionServiceClient _dataAcquisitionClient;

    public SftpFileGateway(IDataAcquisitionServiceClient dataAcquisitionClient)
    {
        _dataAcquisitionClient = dataAcquisitionClient;
    }

    public async Task<ConnectionResult> TestConnectionAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var response = await _dataAcquisitionClient.TestSftpConnectionAsync(facilityId, cancellationToken);

        // 400 here specifically means "no credentials configured yet" (see
        // SftpConfigurationController.TestSftpConnection) — a real, expected outcome to report as
        // a failed test, not a Link-service failure.
        if (response.StatusCode == StatusCodes.Status400BadRequest)
        {
            return new ConnectionResult
            {
                Success = false,
                MessageKey = "onboarding:census.cerner.testFailure",
                Detail = "No credentials are configured for this sFTP connection yet."
            };
        }

        LinkResponseHandler.EnsureSuccess(response, ServiceName, nameof(TestConnectionAsync));

        var wire = string.IsNullOrWhiteSpace(response.RawBody)
            ? null
            : JsonSerializer.Deserialize<SftpConnectionTestResultWire>(response.RawBody, RawBodyOptions);

        return new ConnectionResult
        {
            Success = wire?.Success ?? false,
            MessageKey = wire?.Success == true
                ? "onboarding:census.cerner.testSuccess"
                : "onboarding:census.cerner.testFailure",
            Detail = wire?.Message
        };
    }

    private sealed record SftpConnectionTestResultWire
    {
        public bool Success { get; init; }
        public string? Message { get; init; }
    }
}
