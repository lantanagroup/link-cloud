using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;
using LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link.Mappers;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

// IPatientListGateway over LinkSdk's IDataAcquisitionServiceClient for the FHIR List configuration
// (the six patient list ids), and the patients currently on each list.
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
            EnsureConfigurationSaved(createResponse, facilityId);
            _logger.LogInformation("Created FHIR List configuration for facility {FacilityId}.", facilityId);
            return;
        }

        var updateResponse = await _dataAcquisitionClient.UpdateFhirListConfigurationAsync(payload, cancellationToken);
        EnsureConfigurationSaved(updateResponse, facilityId);
        _logger.LogInformation("Updated FHIR List configuration for facility {FacilityId}.", facilityId);
    }

    // A 400 here is Data Acquisition's own model validation rejecting the payload - most often the
    // FHIR Server Base URL, which it checks more strictly than our own onboarding validation does.
    private static void EnsureConfigurationSaved(LinkApiResponse response, string facilityId)
    {
        if (response.StatusCode == StatusCodes.Status400BadRequest)
        {
            var detail = LinkResponseHandler.ProblemDetail(response.RawBody)
                ?? "The patient list configuration is invalid.";
            throw new InvalidFhirConfigurationException(facilityId, "invalidPatientListConfiguration", detail);
        }

        LinkResponseHandler.EnsureSuccess(response, ServiceName, nameof(SaveConfigurationAsync));
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

    // Data Acquisition reads every configured list from the EHR when includePatients is set, so this
    // is one EHR round trip per list; the other five lists' patients are discarded. An unknown key
    // or an unconfigured list answers with no patients rather than an error.
    public async Task<CensusListResult> QueryAsync(string facilityId, string listKey, CancellationToken cancellationToken = default)
    {
        var wire = await FetchAsync(facilityId, cancellationToken, includePatients: true);
        var patientIds = PatientListConfigurationMapper.FindList(wire, listKey)?.Patients?
            .Select(patient => patient.Id)
            .OfType<string>()
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToArray() ?? [];

        return new CensusListResult
        {
            ListKey = listKey,
            PatientCount = patientIds.Length,
            PatientIds = patientIds
        };
    }

    private async Task<PatientListConfigurationWire?> FetchAsync(string facilityId, CancellationToken cancellationToken, bool includePatients = false)
    {
        var response = await _dataAcquisitionClient.GetFhirListConfigurationAsync(facilityId, includePatients, cancellationToken);

        // Only reachable when includePatients is true - Data Acquisition throws this when one of the
        // facility's lists can't be read from the EHR (most often a FhirId that doesn't exist there).
        // Its own detail names the failing FhirId, which the generic LinkServiceException/502 handler
        // would otherwise discard in favor of "DataAcquisition returned 424."
        if (response.StatusCode == StatusCodes.Status424FailedDependency)
        {
            throw new PatientListRetrievalFailedException(
                facilityId,
                LinkResponseHandler.ProblemDetail(response.RawBody)
                    ?? "One of the facility's patient lists could not be read from the EHR.");
        }

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
