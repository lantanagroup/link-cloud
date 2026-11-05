using LantanaGroup.Link.Census.Application.Factories;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Utilities;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// The contents of one ADT message, stored as the payload of its PatientEvent. Each message type has its own subclass,
/// which says what that type means for the encounter. <see cref="PatientEncounterQueries"/> replays these same methods
/// to rebuild the encounter tables, so they depend only on the payload and the encounter they are given.
/// </summary>
public abstract class AdtPayload : IPayload
{
    public string PayloadType => EventType.ToString();
    public string PatientId { get; set; } = string.Empty;
    public string? MedicalRecordNumber { get; set; }
    public string? EncounterId { get; set; }
    public DateTime? MessageDateTime { get; set; }
    public string? MessagePatientId { get; set; }
    public List<AdtPatientIdentifier> PatientIdentifierList { get; set; } = [];
    public string? AlternatePatientId { get; set; }
    public string? PatientAccountNumber { get; set; }
    public string? PatientClass { get; set; }
    public DateTime? AdmitDateTime { get; set; }
    public DateTime? DischargeDateTime { get; set; }

    /// <summary>The PatientEvent type this payload is stored under.</summary>
    [JsonIgnore]
    public abstract EventType EventType { get; }

    /// <summary>
    /// Whether this message means the patient is in the facility, so that it creates the encounter when none is open.
    /// A feed can start mid-stay, so that first message may not be an A01.
    /// </summary>
    [JsonIgnore]
    public virtual bool ImpliesAdmission => true;

    protected AdtPayload()
    {
    }

    protected AdtPayload(AdtEventEntry entry)
    {
        PatientId = entry.FhirPatientId;
        MedicalRecordNumber = entry.MedicalRecordNumber;
        EncounterId = entry.EncounterId;
        MessageDateTime = entry.MessageDateTime;
        MessagePatientId = entry.PatientId;
        PatientIdentifierList = entry.PatientIdentifierList ?? [];
        AlternatePatientId = entry.AlternatePatientId;
        PatientAccountNumber = entry.PatientAccountNumber;
        PatientClass = entry.PatientClass;
        AdmitDateTime = entry.AdmitDateTime;
        DischargeDateTime = entry.DischargeDateTime;
    }

    public PatientEvent CreatePatientEvent(string facilityId, string correlationId)
    {
        return PatientEventFactory.Create(correlationId, PatientId, EncounterId, MedicalRecordNumber, EventType, this, SourceType.ADT, facilityId);
    }

    public PatientEncounter CreatePatientEncounter(string facilityId, string correlationId)
    {
        var encounter = new PatientEncounterBuilder(
                facilityId,
                MedicalRecordNumber,
                AdmitDateTime ?? MessageDateTime ?? DateTime.UtcNow,
                null,
                correlationId)
            .GetPatientEncounter();

        return UpdatePatientEncounter(encounter);
    }

    public PatientEncounter UpdatePatientEncounter(PatientEncounter patientEncounter)
    {
        patientEncounter.MedicalRecordNumber = MedicalRecordNumber ?? patientEncounter.MedicalRecordNumber;
        patientEncounter.ModifyDate = DateTime.UtcNow;

        SetFhirPatientIdentifier(patientEncounter);

        foreach (var identifier in PatientIdentifierList.Where(x => !IsMedicalRecordNumber(x) && !IsFhirPatientId(x)))
        {
            AddIdentifierIfMissing(patientEncounter, identifier.Id);
        }

        ApplyVisitDetails(patientEncounter);

        ApplyToEncounter(patientEncounter);

        return patientEncounter;
    }

    /// <summary>
    /// Applies what PV1 says about the visit: encounter id.
    /// </summary>
    protected virtual void ApplyVisitDetails(PatientEncounter patientEncounter)
    {
        AddVisitIdentifierIfMissing(patientEncounter);
    }

    /// <summary>
    /// Applies what is specific to this message type. Details common to all of them are already on the encounter.
    /// </summary>
    protected abstract void ApplyToEncounter(PatientEncounter patientEncounter);

    private void SetFhirPatientIdentifier(PatientEncounter patientEncounter)
    {
        var fhirIdentifier = patientEncounter.GetIdentifierByType(SourceType.ADT_FHIR);

        if (fhirIdentifier is null)
        {
            patientEncounter.PatientIdentifiers.Add(NewPatientIdentifier(patientEncounter, PatientId, SourceType.ADT_FHIR));
        }
        else
        {
            // Only a merge changes this: every other message is matched to the encounter by this id
            fhirIdentifier.Identifier = PatientId;
        }
    }

    /// <summary>
    /// An MRN belongs in <see cref="PatientEncounter.MedicalRecordNumber"/>, not in the patient identifiers. A site can
    /// read its MRN from anywhere, so one is recognized by its value as well as by its identifier type.
    /// </summary>
    private bool IsMedicalRecordNumber(AdtPatientIdentifier identifier) =>
        identifier.Id == MedicalRecordNumber
        || string.Equals(identifier.IdentifierType, "MR", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A message can carry its FHIR Patient.Id in PID-3, which is already recorded as the encounter's ADT_FHIR identifier.
    /// </summary>
    private bool IsFhirPatientId(AdtPatientIdentifier identifier) =>
        identifier.Id.SplitReference().Trim() == PatientId;

    private static void AddIdentifierIfMissing(PatientEncounter patientEncounter, string identifier)
    {
        var source = SourceType.ADT.ToString();

        if (!patientEncounter.PatientIdentifiers.Any(x => x.SourceType == source && x.Identifier == identifier))
        {
            patientEncounter.PatientIdentifiers.Add(NewPatientIdentifier(patientEncounter, identifier, SourceType.ADT));
        }
    }

    private void AddVisitIdentifierIfMissing(PatientEncounter patientEncounter)
    {
        if (string.IsNullOrWhiteSpace(EncounterId) || patientEncounter.PatientVisitIdentifiers.Any(x => x.Identifier == EncounterId))
        {
            return;
        }

        patientEncounter.PatientVisitIdentifiers.Add(new PatientVisitIdentifier
        {
            Id = Guid.NewGuid().ToString(),
            PatientEncounterId = patientEncounter.Id,
            Identifier = EncounterId,
            SourceType = SourceType.ADT.ToString(),
            CreateDate = DateTime.UtcNow
        });
    }

    private static PatientIdentifier NewPatientIdentifier(PatientEncounter patientEncounter, string identifier, SourceType sourceType) => new()
    {
        Id = Guid.NewGuid().ToString(),
        PatientEncounterId = patientEncounter.Id,
        Identifier = identifier,
        SourceType = sourceType.ToString(),
        CreateDate = DateTime.UtcNow
    };
}
