using System.Text.Json;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

// ISftpFileGateway over LinkSdk's IDataAcquisitionServiceClient: the saved-configuration test and
// the ad-hoc test that previews the report directory.
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

    public async Task<SftpPreviewResult> TestConnectionWithPreviewAsync(SftpConfig config, CancellationToken cancellationToken = default)
    {
        var request = new SftpTestConnectionRequestApiModel
        {
            HostName = config.Host,
            HostUrlPort = config.Port,
            Username = config.Username ?? string.Empty,
            Password = config.Password ?? string.Empty,
            ReportDirectory = config.RemoteDirectory
        };

        var response = await _dataAcquisitionClient.TestSftpConnectionAsync(request, includeFileContent: true, cancellationToken);

        // 400 means Data Acquisition rejected the connection details before trying them — a failed
        // test to report, not a Link-service failure.
        if (response.StatusCode == StatusCodes.Status400BadRequest)
        {
            return new SftpPreviewResult(
                new ConnectionResult
                {
                    Success = false,
                    MessageKey = "onboarding:census.cerner.testFailure",
                    Detail = LinkResponseHandler.ProblemDetail(response.RawBody)
                },
                []);
        }

        var result = LinkResponseHandler.Require(response, ServiceName, nameof(TestConnectionWithPreviewAsync));

        // The endpoint does not report when it read the directory, so every file is stamped with
        // the time of this call.
        var queriedAt = DateTimeOffset.UtcNow;
        var files = (result.Files ?? [])
            .Select(file => new SftpFile
            {
                FileName = file.FileName,
                QueriedAt = queriedAt,
                PatientIds = file.Patients
                    .Select(patient => patient.PatientId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToList()
            })
            .ToList();

        return new SftpPreviewResult(
            new ConnectionResult
            {
                Success = result.Success,
                MessageKey = result.Success
                    ? "onboarding:census.cerner.testSuccess"
                    : "onboarding:census.cerner.testFailure",
                Detail = result.Message
            },
            files);
    }

    private sealed record SftpConnectionTestResultWire
    {
        public bool Success { get; init; }
        public string? Message { get; init; }
    }
}
