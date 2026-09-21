using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;
using LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link.Mappers;
using LantanaGroup.Link.Sdk.Clients;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

// IPatientListGateway over LinkSdk's IDataAcquisitionServiceClient for the FHIR List configuration
// (the six patient list ids). QueryAsync stays a fixture — see its own interface doc comment.
internal sealed class PatientListGateway : IPatientListGateway
{
    private const string ServiceName = "DataAcquisition";

    private readonly IDataAcquisitionServiceClient _dataAcquisitionClient;
    private readonly IFhirConfigurationGateway _fhirConfigurationGateway;
    private readonly ILogger<PatientListGateway> _logger;

    public PatientListGateway(
        IDataAcquisitionServiceClient dataAcquisitionClient,
        IFhirConfigurationGateway fhirConfigurationGateway,
        ILogger<PatientListGateway> logger)
    {
        _dataAcquisitionClient = dataAcquisitionClient;
        _fhirConfigurationGateway = fhirConfigurationGateway;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetConfigurationAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var wire = await FetchAsync(facilityId, cancellationToken);
        return PatientListConfigurationMapper.ToPatientListIds(wire);
    }

    public async Task SaveConfigurationAsync(string facilityId, IReadOnlyDictionary<string, string> patientListIds, CancellationToken cancellationToken = default)
    {
        await DeleteStaleSftpConfigurationAsync(facilityId, cancellationToken);

        var fhirSection = await _fhirConfigurationGateway.GetAsync(facilityId, cancellationToken);
        if (string.IsNullOrWhiteSpace(fhirSection?.FhirServerBaseUrl))
        {
            throw new InvalidOperationException(
                $"Facility {facilityId} has no FHIR server base URL configured; cannot save the patient list configuration.");
        }

        var existing = await FetchAsync(facilityId, cancellationToken);
        var payload = new PatientListConfigurationPayload
        {
            Id = existing?.Id,
            FacilityId = facilityId,
            FhirBaseServerUrl = fhirSection.FhirServerBaseUrl,
            EHRPatientLists = PatientListConfigurationMapper.ToWireLists(patientListIds)
        };

        if (existing is null)
        {
            var createResponse = await _dataAcquisitionClient.CreateFhirListConfigurationAsync(payload, cancellationToken);
            LinkResponseHandler.EnsureSuccess(createResponse, ServiceName, nameof(SaveConfigurationAsync));
            _logger.LogInformation("Created FHIR List configuration for facility {FacilityId}.", facilityId);
            return;
        }

        var updateResponse = await _dataAcquisitionClient.UpdateFhirListConfigurationAsync(payload, cancellationToken);
        LinkResponseHandler.EnsureSuccess(updateResponse, ServiceName, nameof(SaveConfigurationAsync));
        _logger.LogInformation("Updated FHIR List configuration for facility {FacilityId}.", facilityId);
    }

    public async Task DeleteConfigurationIfExistsAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var existing = await FetchAsync(facilityId, cancellationToken);
        if (existing is null)
        {
            return;
        }

        var response = await _dataAcquisitionClient.DeleteFhirListConfigurationAsync(facilityId, cancellationToken);
        LinkResponseHandler.EnsureSuccess(response, ServiceName, nameof(DeleteConfigurationIfExistsAsync));
        _logger.LogInformation("Deleted FHIR List configuration for facility {FacilityId}.", facilityId);
    }

    // Fixture-only: see IPatientListGateway.QueryAsync's doc comment. No SDK call — Data
    // Acquisition's read endpoint doesn't return matched patients yet.
    public Task<CensusListResult> QueryAsync(string facilityId, string listKey, CancellationToken cancellationToken = default)
    {
        var patientIds = Enumerable.Range(1, Random.Shared.Next(5, 51))
            .Select(i => $"SIMULATED-PATIENT-{i:D4}")
            .ToArray();

        return Task.FromResult(new CensusListResult
        {
            ListKey = listKey,
            PatientCount = patientIds.Length,
            PatientIds = patientIds,
            Simulated = true
        });
    }

    private async Task<PatientListConfigurationWire?> FetchAsync(string facilityId, CancellationToken cancellationToken)
    {
        var response = await _dataAcquisitionClient.GetFhirListConfigurationAsync(facilityId, cancellationToken: cancellationToken);
        return LinkResponseHandler.OptionalFromRawBody<PatientListConfigurationWire>(response, ServiceName, nameof(GetConfigurationAsync));
    }

    // A facility can only have one census acquisition method in Data Acquisition (sFTP or FHIR
    // List) — creating a FHIR List configuration while a stale sFTP configuration still exists
    // (e.g. the facility switched from Cerner to Epic) fails with EntityAlreadyExistsException.
    private async Task DeleteStaleSftpConfigurationAsync(string facilityId, CancellationToken cancellationToken)
    {
        var response = await _dataAcquisitionClient.GetOrganizationSftpConfigurationAsync(facilityId, cancellationToken);
        var existing = LinkResponseHandler.OptionalFromRawBody<SftpConfigurationIdWire>(response, ServiceName, nameof(SaveConfigurationAsync));
        if (existing?.Id is null)
        {
            return;
        }

        var deleteResponse = await _dataAcquisitionClient.DeleteSftpConfigurationAsync(facilityId, existing.Id, cancellationToken);
        LinkResponseHandler.EnsureSuccess(deleteResponse, ServiceName, nameof(SaveConfigurationAsync));
        _logger.LogInformation("Deleted stale sFTP configuration for facility {FacilityId} before saving its FHIR List configuration.", facilityId);
    }
}

internal sealed record SftpConfigurationIdWire
{
    public string? Id { get; init; }
}
