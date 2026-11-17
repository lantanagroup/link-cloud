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
/// <item>A08, A11, A23, merges and visit changes never imply a stay, and nothing but an A01, A03, A04, A06, A07 or A13 that
/// opens a stay, or an A03 that closes one, emits anything: downstream has no way to take back an Admit or Discharge it has
/// already received.</item>
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

                switch (payload)
                {
                    case AdtPatientMergePayload merge:
                        await ProcessMerge(facilityId, merge, cancellationToken);
                        break;
                    case AdtVisitChangePayload visitChange:
                        await ProcessVisitChange(facilityId, visitChange, cancellationToken);
                        break;
                    default:
                        await ProcessEncounterEvent(facilityId, payload, messages, cancellationToken);
                        break;
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
                // An A08 is sent for every kind of visit, most of which Census never tracks
                _logger.Log(payload is AdtA08Payload ? LogLevel.Debug : LogLevel.Warning,
                    "ADT {EventType} message for facility {FacilityId} matched no encounter", payload.EventType, facilityId.SanitizeForLog());
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
    private async Task ProcessMerge(string facilityId, AdtPatientMergePayload payload, CancellationToken cancellationToken)
    {
        var encounters = await GetEncountersByIdentifiers(facilityId, payload.GetPriorIdentifiers(), cancellationToken);

        if (encounters.Count == 0)
        {
            _logger.LogWarning("ADT {EventType} message for facility {FacilityId} matched no encounter", payload.EventType, facilityId.SanitizeForLog());
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
    /// A visit change applies to the one encounter its prior visit number names: the patient's own for a merge or change of
    /// visit number, the prior patient's for a move. It opens nothing and emits nothing. One already applied, so that the
    /// encounter carries the new number, is applied again harmlessly.
    /// </summary>
    private async Task ProcessVisitChange(string facilityId, AdtVisitChangePayload payload, CancellationToken cancellationToken)
    {
        var encounters = payload is AdtA45Payload move
            ? await GetEncountersByIdentifiers(facilityId, move.GetPriorIdentifiers(), cancellationToken)
            : await _patientEncounterQueries.GetTrackedEncountersByFhirPatientId(facilityId, payload.PatientId, cancellationToken);

        var priorVisitIds = payload.GetVisitIdentifiersToMatch();
        var encounter = encounters.FirstOrDefault(e => e.PatientVisitIdentifiers.Any(v => priorVisitIds.Contains(v.Identifier)))
            ?? (string.IsNullOrWhiteSpace(payload.EncounterId) || payload is AdtA45Payload
                ? null
                : encounters.FirstOrDefault(e => e.PatientVisitIdentifiers.Any(v => v.Identifier == payload.EncounterId)));

        if (encounter is null)
        {
            _logger.LogWarning("ADT {EventType} message for facility {FacilityId} matched no encounter", payload.EventType, facilityId.SanitizeForLog());
            await _patientEventManager.AddPatientEvent(payload.CreatePatientEvent(facilityId, Guid.NewGuid().ToString()), cancellationToken);
            return;
        }

        // A visit merged into one tracked as an encounter of its own: the prior encounter is closed, and the surviving one
        // takes the prior visit numbers
        if (payload is AdtA42Payload visitMerge
            && encounters.FirstOrDefault(e => e != encounter && e.PatientVisitIdentifiers.Any(v => v.Identifier == payload.EncounterId)) is { } surviving)
        {
            var mergedAway = visitMerge.ForMergedAwayEncounter();
            await _patientEventManager.AddPatientEvent(mergedAway.CreatePatientEvent(facilityId, encounter.CorrelationId), cancellationToken);
            mergedAway.UpdatePatientEncounter(encounter);
            encounter = surviving;
        }

        await _patientEventManager.AddPatientEvent(payload.CreatePatientEvent(facilityId, encounter.CorrelationId), cancellationToken);
        payload.UpdatePatientEncounter(encounter);
        await _patientEncounterQueries.SaveTrackedChanges(cancellationToken);
    }

    private async Task<List<PatientEncounter>> GetEncountersByIdentifiers(string facilityId, IReadOnlyCollection<string> identifiers, CancellationToken cancellationToken) =>
        identifiers.Count == 0
            ? []
            : await _patientEncounterQueries.GetTrackedEncountersByIdentifiers(facilityId, identifiers, cancellationToken);

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
