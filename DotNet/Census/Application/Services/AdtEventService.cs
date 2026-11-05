using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Payloads.Adt;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.Census.Application.Services;

public interface IAdtEventService
{
    /// <summary>
    /// Records the tracked messages of an ADTAcquired event, in order, as PatientEvents and updates the encounter tables.
    /// </summary>
    /// <returns>The PatientEvent Kafka messages to produce once the changes are committed.</returns>
    Task<List<IBaseResponse>> ProcessEvents(string facilityId, ADTAcquired adtAcquired, CancellationToken cancellationToken);
}

/// <summary>
/// Applies an ADT feed to the census. Only an admission and a discharge reach the rest of the pipeline:
/// <list type="bullet">
/// <item>The first tracked message of a stay emits Admit, whatever its type, because a feed can start mid-stay. A later
/// A01 for that open stay fills in its details but does not emit a second Admit.</item>
/// <item>An A03 emits Discharge, so an A03 that is also the first message of a stay emits Admit and Discharge.</item>
/// <item>A11 and A18 never imply a stay, and A11, A13 and A18 emit nothing: downstream has no way to take back an
/// Admit or Discharge it has already received.</item>
/// </list>
/// </summary>
public class AdtEventService : IAdtEventService
{
    private readonly ILogger<AdtEventService> _logger;
    private readonly ICensusServiceMetrics _metrics;
    private readonly IPatientEventManager _patientEventManager;
    private readonly IPatientEventQueries _patientEventQueries;
    private readonly IPatientEncounterQueries _patientEncounterQueries;
    private readonly IPatientEncounterManager _patientEncounterManager;

    public AdtEventService(
        ILogger<AdtEventService> logger,
        ICensusServiceMetrics metrics,
        IPatientEventManager patientEventManager,
        IPatientEventQueries patientEventQueries,
        IPatientEncounterQueries patientEncounterQueries,
        IPatientEncounterManager patientEncounterManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _patientEventManager = patientEventManager ?? throw new ArgumentNullException(nameof(patientEventManager));
        _patientEventQueries = patientEventQueries ?? throw new ArgumentNullException(nameof(patientEventQueries));
        _patientEncounterQueries = patientEncounterQueries ?? throw new ArgumentNullException(nameof(patientEncounterQueries));
        _patientEncounterManager = patientEncounterManager ?? throw new ArgumentNullException(nameof(patientEncounterManager));
    }

    /// <inheritdoc/>
    public async Task<List<IBaseResponse>> ProcessEvents(string facilityId, ADTAcquired adtAcquired, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            throw new ArgumentException("FacilityId cannot be null or empty", nameof(facilityId));
        }

        var messages = new List<IBaseResponse>();

        if (adtAcquired.Events is null || adtAcquired.Events.Count == 0)
        {
            return messages;
        }

        await using var transaction = await _patientEventQueries.StartTransaction(cancellationToken);

        try
        {
            foreach (var entry in adtAcquired.Events)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var payload = AdtPayloadFactory.Create(entry);
                if (payload is null)
                {
                    _logger.LogDebug("Ignoring ADT message type {MessageType} for facility {FacilityId}", entry.MessageType.SanitizeForLog(), facilityId.SanitizeForLog());
                    continue;
                }

                if (string.IsNullOrWhiteSpace(payload.PatientId))
                {
                    _logger.LogError("Ignoring ADT {MessageType} message with no FHIR Patient.Id for facility {FacilityId}", entry.MessageType.SanitizeForLog(), facilityId.SanitizeForLog());
                    continue;
                }

                // A file can be read twice, such as when moving it failed after producing its events
                if (payload.MessageDateTime is { } messageDateTime
                    && await _patientEventQueries.AdtEventExists(facilityId, payload.PatientId, payload.EventType, messageDateTime, payload.EncounterId, cancellationToken))
                {
                    _logger.LogDebug("Ignoring repeated ADT {MessageType} message for facility {FacilityId}", entry.MessageType.SanitizeForLog(), facilityId.SanitizeForLog());
                    continue;
                }

                if (payload is AdtA18Payload merge)
                {
                    await ProcessMerge(facilityId, merge, cancellationToken);
                }
                else
                {
                    await ProcessEncounterEvent(facilityId, payload, messages, cancellationToken);
                }
            }

            await _patientEventQueries.CommitTransaction(transaction, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ADT events for facility {FacilityId}", facilityId.SanitizeForLog());
            await _patientEventQueries.RollbackTransaction(transaction, cancellationToken);
            throw;
        }

        return messages;
    }

    private async Task ProcessEncounterEvent(string facilityId, AdtPayload payload, List<IBaseResponse> messages, CancellationToken cancellationToken)
    {
        var encounters = await _patientEncounterQueries.GetTrackedEncountersByFhirPatientId(facilityId, payload.PatientId, cancellationToken);
        var encounter = FindEncounter(encounters, payload);

        if (encounter is null)
        {
            var correlationId = Guid.NewGuid().ToString();

            await _patientEventManager.AddPatientEvent(payload.CreatePatientEvent(facilityId, correlationId), cancellationToken);

            if (!payload.ImpliesAdmission)
            {
                _logger.LogWarning("ADT {EventType} message for facility {FacilityId} matched no encounter", payload.EventType, facilityId.SanitizeForLog());
                return;
            }

            await _patientEncounterManager.AddPatientEncounterAsync(payload.CreatePatientEncounter(facilityId, correlationId), cancellationToken);

            AddMessage(messages, facilityId, payload.PatientId, correlationId, PatientEvents.Admit);

            if (payload is AdtA03Payload)
            {
                AddMessage(messages, facilityId, payload.PatientId, correlationId, PatientEvents.Discharge);
            }

            return;
        }

        var wasOpen = encounter.DischargeDate is null;

        await _patientEventManager.AddPatientEvent(payload.CreatePatientEvent(facilityId, encounter.CorrelationId), cancellationToken);

        payload.UpdatePatientEncounter(encounter);
        await _patientEncounterQueries.SaveTrackedChanges(cancellationToken);

        if (payload is AdtA03Payload && wasOpen)
        {
            AddMessage(messages, facilityId, payload.PatientId, encounter.CorrelationId, PatientEvents.Discharge);
        }
    }

    /// <summary>
    /// A merge updates every encounter of the patient merged away, with one PatientEvent per encounter so that each can
    /// be rebuilt from its own events. It has no stay of its own, so it emits nothing and opens nothing.
    /// </summary>
    private async Task ProcessMerge(string facilityId, AdtA18Payload payload, CancellationToken cancellationToken)
    {
        var priorIdentifiers = payload.GetPriorIdentifiers();
        var encounters = priorIdentifiers.Count == 0
            ? []
            : await _patientEncounterQueries.GetTrackedEncountersByIdentifiers(facilityId, priorIdentifiers, cancellationToken);

        if (encounters.Count == 0)
        {
            _logger.LogWarning("ADT A18 message for facility {FacilityId} matched no encounter", facilityId.SanitizeForLog());
            await _patientEventManager.AddPatientEvent(payload.CreatePatientEvent(facilityId, Guid.NewGuid().ToString()), cancellationToken);
            return;
        }

        foreach (var encounter in encounters)
        {
            await _patientEventManager.AddPatientEvent(payload.CreatePatientEvent(facilityId, encounter.CorrelationId), cancellationToken);
            payload.UpdatePatientEncounter(encounter);
        }

        await _patientEncounterQueries.SaveTrackedChanges(cancellationToken);
    }

    /// <summary>
    /// Finds the stay a message belongs to, from the patient's encounters with the most recently admitted first.
    /// </summary>
    private static PatientEncounter? FindEncounter(List<PatientEncounter> encounters, AdtPayload payload)
    {
        if (!string.IsNullOrWhiteSpace(payload.EncounterId))
        {
            return encounters.FirstOrDefault(e => e.PatientVisitIdentifiers.Any(v => v.Identifier == payload.EncounterId))
                // A stay first seen in a message that carried no encounter id
                ?? encounters.FirstOrDefault(e => e.DischargeDate is null && e.PatientVisitIdentifiers.Count == 0);
        }

        // Only cancelling a discharge can apply to a stay that has ended
        return payload is AdtA13Payload
            ? encounters.FirstOrDefault()
            : encounters.FirstOrDefault(e => e.DischargeDate is null);
    }

    private void AddMessage(List<IBaseResponse> messages, string facilityId, string patientId, string correlationId, PatientEvents eventType)
    {
        messages.Add(new PatientEventResponse
        {
            CorrelationId = correlationId,
            FacilityId = facilityId,
            TopicName = KafkaTopic.PatientEvent.ToString(),
            PatientEvent = new Models.Messages.PatientEvent
            {
                PatientId = patientId,
                EventType = eventType.ToString()
            }
        });

        List<KeyValuePair<string, object?>> tags =
        [
            new(DiagnosticNames.FacilityId, facilityId),
            new(DiagnosticNames.PatientEvent, eventType.ToString())
        ];

        if (eventType == PatientEvents.Admit)
        {
            _metrics.IncrementPatientAdmittedCounter(tags);
        }
        else
        {
            _metrics.IncrementPatientDischargedCounter(tags);
        }
    }
}
