using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.DataAcq;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Census.Application.Validators;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Census.Application.Services;

public interface IPatientListService
{
    Task<List<IBaseResponse>> ProcessLists(string facilityId, List<PatientListItem> lists,
        CancellationToken cancellationToken);

    Task<List<IBaseResponse>> ProcessList(string facilityId, PatientListItem list, CancellationToken cancellationToken);
}

public class PatientListService : IPatientListService
{
    private readonly ILogger<PatientListService> _logger;
    private readonly ICensusServiceMetrics _metrics;
    private readonly IPatientEventManager _patientEventManager;
    private readonly IPatientEventQueries _patientEventQueries;
    private readonly IPatientEncounterQueries _patientEncounterQueries;
    private readonly IPatientEncounterManager _patientEncounterManager;
    private readonly ICensusConfigManager _censusConfigManager;

    public PatientListService(
        ILogger<PatientListService> logger,
        ICensusServiceMetrics metrics,
        IPatientEventQueries patientEventQueries,
        IPatientEventManager patientEventManager,
        IPatientEncounterQueries patientEncounterQueries,
        IPatientEncounterManager patientEncounterManager,
        ICensusConfigManager censusConfigManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _patientEventQueries = patientEventQueries ?? throw new ArgumentNullException(nameof(patientEventQueries));
        _patientEventManager = patientEventManager ?? throw new ArgumentNullException(nameof(patientEventManager));
        _patientEncounterQueries =
            patientEncounterQueries ?? throw new ArgumentNullException(nameof(patientEncounterQueries));
        _patientEncounterManager =
            patientEncounterManager ?? throw new ArgumentNullException(nameof(patientEncounterManager));
        _censusConfigManager = censusConfigManager ?? throw new ArgumentNullException(nameof(censusConfigManager));
    }

    public async Task<List<IBaseResponse>> ProcessList(string facilityId, PatientListItem list,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentException("FacilityId cannot be null or empty", nameof(facilityId));
        if (list == null)
            throw new ArgumentNullException(nameof(list));

        //return empty list if no patient ids
        if (list.PatientIds == null || !list.PatientIds.Any())
            return new List<IBaseResponse>();

        //ensure valid facility by checking if census configuration exists:
        if (await _censusConfigManager.GetCensusConfigByFacilityId(facilityId) == null)
        {
            throw new ArgumentException($"Census configuration does not exist for facility {facilityId}.");
        }

        List<IBaseResponse> messages = new List<IBaseResponse>();
        foreach (var patientId in list.PatientIds)
        {
            await using var transaction = await _patientEventQueries.StartTransaction(cancellationToken);

            try
            {
                var existingEvent =
                    await _patientEventQueries.GetLatestEventByFacilityAndPatientId(facilityId, patientId,
                        cancellationToken);

                bool shouldSkip = false;
                if (existingEvent != null)
                {
                    var skipProcessing = ShouldSkipProcessing(patientId, facilityId, existingEvent, list.ListType);
                    if (skipProcessing.result)
                    {
                        _logger.LogInformation(
                            "{SkipMessage} PatientId: {PatientId}, FacilityId: {FacilityId}, EventType: {EventType}, ListType: {ListType}",
                            skipProcessing.message,
                            patientId,
                            facilityId,
                            "Admit",
                            list.ListType);

                        shouldSkip = true; // Mark for skipping but don't continue yet
                    }
                }

                if (shouldSkip)
                {
                    continue; // Skip processing for this patient
                }

                var sharedCorrelationId = existingEvent != null
                    && existingEvent.EventType == EventType.FHIRListDischarge
                    && list.ListType == ListType.Admit
                    ? Guid.NewGuid().ToString() : (existingEvent?.CorrelationId ?? Guid.NewGuid().ToString());

                await EnsureAdmitEventExists(facilityId, patientId, sharedCorrelationId, list.ListType,
                    existingEvent,
                    cancellationToken);

                IPayload payload = list.ListType == ListType.Admit
                    ? new FHIRListAdmitPayload(patientId, DateTime.UtcNow)
                    : new FHIRListDischargePayload(patientId, DateTime.UtcNow);

                var patientEvent = payload.CreatePatientEvent(facilityId, sharedCorrelationId);

                await _patientEventManager.AddPatientEvent(patientEvent, cancellationToken);

                if (list.ListType == ListType.Discharge)
                {
                    PatientEncounter encounter =
                        await _patientEncounterQueries.GetPatientEncounterByCorrelationIdAsync(sharedCorrelationId,
                            cancellationToken);

                    if (encounter == null)
                    {
                        var admitPayload = new FHIRListAdmitPayload(patientId, DateTime.UtcNow);
                        var patientEncounter = admitPayload.CreatePatientEncounter(facilityId, sharedCorrelationId);
                        encounter = await _patientEncounterManager.AddPatientEncounterAsync(patientEncounter,
                            cancellationToken);
                    }

                    encounter = payload.UpdatePatientEncounter(encounter);
                    await _patientEncounterManager.UpdatePatientEncounterAsync(encounter, cancellationToken);

                    messages.Add(new PatientEventResponse
                    {
                        CorrelationId = sharedCorrelationId,
                        FacilityId = facilityId,
                        TopicName = KafkaTopic.PatientEvent.ToString(),
                        PatientEvent = new Models.Messages.PatientEvent
                        {
                            PatientId = patientId,
                            EventType = PatientEvents.Discharge.ToString()
                        }
                    });

                    _metrics.IncrementPatientDischargedCounter([
                        new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId),
                        new KeyValuePair<string, object?>(DiagnosticNames.PatientId, patientId),
                        new KeyValuePair<string, object?>(DiagnosticNames.PatientEvent,
                            PatientEvents.Discharge.ToString()),
                        new KeyValuePair<string, object?>(DiagnosticNames.CorrelationId, sharedCorrelationId)
                    ]);
                }
                else
                {
                    _metrics.IncrementPatientAdmittedCounter([
                        new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId),
                        new KeyValuePair<string, object?>(DiagnosticNames.PatientId, patientId),
                        new KeyValuePair<string, object?>(DiagnosticNames.PatientEvent,
                            PatientEvents.Admit.ToString())
                    ]);

                    var patientEncounter = payload.CreatePatientEncounter(facilityId, sharedCorrelationId);
                    await _patientEncounterManager.AddPatientEncounterAsync(patientEncounter, cancellationToken);
                }

                await _patientEventQueries.CommitTransaction(transaction, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing patient list for facility {FacilityId}", facilityId);
                await _patientEventQueries.RollbackTransaction(transaction, cancellationToken);
                throw;
            }
        }

        return messages;
    }

    private async Task EnsureAdmitEventExists(string facilityId, string patientId, string correlationId,
        ListType listType, PatientEvent? existingEvent = default, CancellationToken cancellationToken = default)
    {
        if (existingEvent == null && listType == ListType.Discharge)
        {
            //create and add an admit event
            var payload = new FHIRListAdmitPayload(patientId, DateTime.UtcNow);
            var admitEvent = payload.CreatePatientEvent(facilityId, correlationId);

            try
            {
                await _patientEventManager.AddPatientEvent(admitEvent, cancellationToken);
                _logger.LogInformation("Added admit event for patient {patientId} in facility {facilityId}", patientId,
                    facilityId);

                PatientEncounter encounter =
                        await _patientEncounterQueries.GetPatientEncounterByCorrelationIdAsync(correlationId,
                            cancellationToken);

                if (encounter == null)
                {
                    var patientEncounter = payload.CreatePatientEncounter(facilityId, correlationId);
                    encounter = await _patientEncounterManager.AddPatientEncounterAsync(patientEncounter,
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding admit event for patient {patientId} in facility {facilityId}",
                    patientId, facilityId);
                throw;
            }
        }
    }

    private (bool result, string message) ShouldSkipProcessing(string patientId, string facilityId,
        PatientEvent? existingEvent, ListType listType)
    {
        (bool result, string message) results = (false, string.Empty);
        if (existingEvent != null && existingEvent.EventType == EventType.FHIRListAdmit && listType == ListType.Admit)
        {
            results.result = true;
            results.message =
                "Patient event for {patientId} for FhirListAdmit already exists in facility {facilityId}. Skipping.";
        }

        if (existingEvent != null && existingEvent.EventType == EventType.FHIRListDischarge &&
            listType == ListType.Discharge)
        {
            // If the event already exists, we can skip processing
            results.result = true;
            results.message =
                "Patient event for {patientId} for FhirListDischarge already exists in facility {facilityId}. Skipping.";
        }

        return results;
    }


    public async Task<List<IBaseResponse>> ProcessLists(string facilityId, List<PatientListItem> lists,
        CancellationToken cancellationToken)
    {
        var validationResults = PatientListsValidator.ValidatePatientLists(lists);

        if (!validationResults.success)
        {
            throw new DeadLetterException(
                $"Error(s) validating lists:\n\t{string.Join("\n\t\t", validationResults.validationErrors)}");
        }

        List<IBaseResponse> messages = new List<IBaseResponse>();

        //Fetch currently list of admitted patients and determine which patients
        //do not appear on the incoming lists. Any such patients should be discharged.
        var flattenedIncomingPatientIds = lists
            .SelectMany(l => l.PatientIds)
            .Distinct()
            .ToHashSet();

        var currentlyAdmittedPatients = await _patientEncounterQueries.GetCurrentlyAdmittedPatientsForFacility(facilityId, cancellationToken);

        var patientsToDischarge = currentlyAdmittedPatients
            .Where(pe => !flattenedIncomingPatientIds.Contains(pe))
            .Select(pe => pe)
            .ToList();

        if (patientsToDischarge.Any())
        {
            foreach(var patientId in patientsToDischarge)
            {
                var dischargeList = new PatientListItem
                {
                    ListType = ListType.Discharge,
                    PatientIds = new List<string> { patientId }
                };
                messages.AddRange(await ProcessList(facilityId, dischargeList, cancellationToken));
            }
        }

        foreach (var list in lists.OrderBy(x => x.ListType))
        {
            messages.AddRange(await ProcessList(facilityId, list, cancellationToken));
        }

        return messages;
    }
}