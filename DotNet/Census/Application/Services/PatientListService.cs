using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.DataAcq;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;

namespace LantanaGroup.Link.Census.Application.Services;

public interface IPatientListService
{
    Task<List<IBaseResponse>> ProcessLists(string facilityId, List<PatientListItem> lists, CancellationToken cancellationToken);
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

    public PatientListService(
        ILogger<PatientListService> logger,
        ICensusServiceMetrics metrics,
        IPatientEventQueries patientEventQueries,
        IPatientEventManager patientEventManager,
        IPatientEncounterQueries patientEncounterQueries,
        IPatientEncounterManager patientEncounterManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _patientEventQueries = patientEventQueries ?? throw new ArgumentNullException(nameof(patientEventQueries));
        _patientEventManager = patientEventManager ?? throw new ArgumentNullException(nameof(patientEventManager));
        _patientEncounterQueries = patientEncounterQueries ?? throw new ArgumentNullException(nameof(patientEncounterQueries));
        _patientEncounterManager = patientEncounterManager ?? throw new ArgumentNullException(nameof(patientEncounterManager));
    }

    public async Task<List<IBaseResponse>> ProcessList(string facilityId, PatientListItem list, CancellationToken cancellationToken)
    {
        List<IBaseResponse> messages = new List<IBaseResponse>();
        foreach (var patientId in list.PatientIds)
        {
            var existingEvent = await _patientEventQueries.GetLatestEventByFacilityAndPatientId(facilityId, patientId, cancellationToken);
            string sharedCorrelationId = null;

            if (existingEvent != null && existingEvent.EventType == EventType.FHIRListAdmit && list.ListType == ListType.Admit)
            {
                _logger.LogInformation("Patient event for {patientId} for FhirListAdmit already exists in facility {facilityId}. Skipping.", patientId, facilityId);
                continue;
            }

            if(existingEvent != null && existingEvent.EventType == EventType.FHIRListDischarge && list.ListType == ListType.Discharge)
            {
                // If the event already exists, we can skip processing
                _logger.LogInformation("Patient event for {patientId} for FhirListDischarge already exists in facility {facilityId}. Skipping.", patientId, facilityId);
                continue;
            }

            if(existingEvent == null && list.ListType == ListType.Discharge)
            {
                sharedCorrelationId = Guid.NewGuid().ToString();
                //create and add an admit event
                var admitEvent = new FHIRListAdmitPayload(patientId, DateTime.UtcNow).CreatePatientEvent(facilityId, sharedCorrelationId);
                try
                {
                    var addedAdmitEvent = await _patientEventManager.AddPatientEvent(admitEvent, cancellationToken);
                    _logger.LogInformation("Added admit event for patient {patientId} in facility {facilityId}", patientId, facilityId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error adding admit event for patient {patientId} in facility {facilityId}", patientId, facilityId);
                    throw;
                }
            }

            sharedCorrelationId = existingEvent?.CorrelationId ?? sharedCorrelationId ?? Guid.NewGuid().ToString();

            IPayload payload = list.ListType == ListType.Admit
                ? new FHIRListAdmitPayload(patientId, DateTime.UtcNow)
                : new FHIRListDischargePayload(patientId, DateTime.UtcNow);

            var patientEvent = payload.CreatePatientEvent(facilityId, sharedCorrelationId);

            try
            {
                var addedEvent = await _patientEventManager.AddPatientEvent(patientEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing patient list for facility {facilityId} and patient {patientId}", facilityId, patientId);
                throw;
            }

            if (list.ListType == ListType.Discharge)
            {
                PatientEncounter encounter = await _patientEncounterQueries.GetPatientEncounterByCorrelationIdAsync(sharedCorrelationId, cancellationToken);

                if (encounter == null)
                {
                    var admitPayload = new FHIRListAdmitPayload(patientId, DateTime.UtcNow);
                    var patientEncounter = admitPayload.CreatePatientEncounter(facilityId, sharedCorrelationId);
                    encounter = await _patientEncounterManager.AddPatientEncounterAsync(patientEncounter, cancellationToken);
                }

                encounter = payload.UpdatePatientEncounter(encounter);
                await _patientEncounterManager.UpdatePatientEncounterAsync(encounter, cancellationToken);

                messages.Add(new PatientEventResponse { CorrelationId = sharedCorrelationId, FacilityId = facilityId, TopicName = KafkaTopic.PatientEvent.ToString(), PatientEvent = new Models.Messages.PatientEvent { PatientId = patientId, EventType = PatientEvents.Discharge.ToString() } });

                _metrics.IncrementPatientDischargedCounter([
                    new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId),
                    new KeyValuePair<string, object?>(DiagnosticNames.PatientId, patientId),
                    new KeyValuePair<string, object?>(DiagnosticNames.PatientEvent, PatientEvents.Discharge.ToString()),
                    new KeyValuePair<string, object?>(DiagnosticNames.CorrelationId, sharedCorrelationId)
                ]);
            }
            else
            {
                _metrics.IncrementPatientAdmittedCounter([
                    new KeyValuePair<string, object?>(DiagnosticNames.FacilityId,facilityId),
                    new KeyValuePair<string, object?>(DiagnosticNames.PatientId, patientId),
                    new KeyValuePair<string, object?>(DiagnosticNames.PatientEvent, PatientEvents.Admit.ToString())
                ]);

                var patientEncounter = payload.CreatePatientEncounter(facilityId, sharedCorrelationId);
                await _patientEncounterManager.AddPatientEncounterAsync(patientEncounter, cancellationToken);
            }
        }
        return messages;
    }

    public async Task<List<IBaseResponse>> ProcessLists(string facilityId, List<PatientListItem> lists, CancellationToken cancellationToken)
    {
        List<IBaseResponse> messages = new List<IBaseResponse>();
        foreach (var list in lists)
        {
            try
            {
                messages.AddRange(await ProcessList(facilityId, list, cancellationToken));
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error processing patient list for facility {facilityId}", facilityId);
                // Optionally, you can handle specific exceptions or rethrow them
                throw;
            }
        }
        return messages;
    }
}
