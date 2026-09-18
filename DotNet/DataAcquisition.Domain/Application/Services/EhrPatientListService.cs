using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Logging;
using List = Hl7.Fhir.Model.List;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

public interface IEhrPatientListService
{
    /// <summary>
    /// Reads every List configured in <paramref name="listConfiguration"/> from the facility's EHR and
    /// sets <see cref="EhrPatientListModel.Patients"/> on each entry. Any failure aborts the whole
    /// operation - the configuration is never returned partially populated.
    /// </summary>
    Task PopulatePatientsAsync(FhirListConfigurationModel listConfiguration, CancellationToken cancellationToken = default);
}

public class EhrPatientListService : IEhrPatientListService
{
    private readonly ILogger<EhrPatientListService> _logger;
    private readonly IFhirQueryConfigurationQueries _fhirQueryConfigurationQueries;
    private readonly IReadFhirCommand _readFhirCommand;

    public EhrPatientListService(
        ILogger<EhrPatientListService> logger,
        IFhirQueryConfigurationQueries fhirQueryConfigurationQueries,
        IReadFhirCommand readFhirCommand)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fhirQueryConfigurationQueries = fhirQueryConfigurationQueries ?? throw new ArgumentNullException(nameof(fhirQueryConfigurationQueries));
        _readFhirCommand = readFhirCommand ?? throw new ArgumentNullException(nameof(readFhirCommand));
    }

    public async Task PopulatePatientsAsync(FhirListConfigurationModel listConfiguration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listConfiguration);

        using var activity = ServiceActivitySource.Instance.StartActivity("EhrPatientListService.PopulatePatientsAsync");
        activity?.SetTag(DiagnosticNames.FacilityId, listConfiguration.FacilityId);

        var configuredLists = listConfiguration.EHRPatientLists;

        if (configuredLists == null || configuredLists.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(listConfiguration.FhirBaseServerUrl))
        {
            throw new MissingFacilityConfigurationException(
                $"FhirBaseServerUrl is not configured for facility {listConfiguration.FacilityId.SanitizeAndRemove()}. Patients cannot be retrieved.");
        }

        // ReadFhirCommand builds the EHR credentials from the FhirQueryConfiguration, not from the
        // list configuration, so without it not a single list can be read.
        var fhirQueryConfig = await _fhirQueryConfigurationQueries.GetByFacilityIdAsync(listConfiguration.FacilityId, cancellationToken);

        if (fhirQueryConfig == null)
        {
            throw new MissingFacilityConfigurationException(
                $"Missing FHIR query configuration for facility {listConfiguration.FacilityId.SanitizeAndRemove()}. Patients cannot be retrieved.");
        }

        // Read sequentially: ReadFhirCommand already serialises these behind a per-facility distributed
        // semaphore whose default size is 1, so fanning out would only add lock contention.
        foreach (var configuredList in configuredLists)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(configuredList.FhirId))
            {
                throw new PatientListRetrievalFailedException(
                    $"A patient list configured for facility {listConfiguration.FacilityId.SanitizeAndRemove()} has no FhirId. Patients cannot be retrieved.");
            }

            configuredList.Patients = await ReadPatientsAsync(listConfiguration, configuredList.FhirId, fhirQueryConfig, cancellationToken);
        }
    }

    private async Task<List<EhrPatientListPatientModel>> ReadPatientsAsync(
        FhirListConfigurationModel listConfiguration,
        string listFhirId,
        FhirQueryConfigurationModel fhirQueryConfig,
        CancellationToken cancellationToken)
    {
        DomainResource? resource;

        try
        {
            resource = await _readFhirCommand.ExecuteAsync(
                new ReadFhirCommandRequest(
                    listConfiguration.FacilityId,
                    ResourceType.List,
                    listFhirId,
                    listConfiguration.FhirBaseServerUrl,
                    fhirQueryConfig,
                    null),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TooManyRequestsException)
        {
            // Surfaced separately so the caller can answer with a Retry-After.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading FHIR List {listId} for facility {facilityId}.",
                listFhirId.SanitizeForLog(), listConfiguration.FacilityId.SanitizeForLog());

            throw new PatientListRetrievalFailedException(
                $"Unable to read list {listFhirId.SanitizeAndRemove()} from the EHR for facility {listConfiguration.FacilityId.SanitizeAndRemove()}.", ex);
        }

        if (resource is not List fhirList)
        {
            _logger.LogError("FHIR List {listId} for facility {facilityId} returned {resourceType} instead of a List.",
                listFhirId.SanitizeForLog(), listConfiguration.FacilityId.SanitizeForLog(), resource?.TypeName ?? "null");

            throw new PatientListRetrievalFailedException(
                $"The EHR did not return a List resource for list {listFhirId.SanitizeAndRemove()} for facility {listConfiguration.FacilityId.SanitizeAndRemove()}.");
        }

        return (fhirList.Entry ?? [])
            .Select(entry => new EhrPatientListPatientModel
            {
                Id = entry.Item?.Reference?.SplitReference().Trim(),
                Name = string.IsNullOrWhiteSpace(entry.Item?.Display) ? null : entry.Item.Display.Trim()
            })
            .ToList();
    }
}
