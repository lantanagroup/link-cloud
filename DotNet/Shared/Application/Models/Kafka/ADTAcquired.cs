namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

/// <summary>
/// Value of the <c>ADTAcquired</c> Kafka event, keyed by facility id. Normally one event per SFTP file; a file too
/// large for one Kafka message is split across several events, produced in order under the same key.
/// </summary>
public class ADTAcquired
{
    /// <summary>
    /// The ADT messages acquired, in the order they were read from the file.
    /// </summary>
    public List<AdtEventEntry> Events { get; set; } = [];
}

/// <summary>
/// The elements of one HL7 v2 ADT message that Link consumes, plus the identifiers resolved from it.
/// </summary>
public class AdtEventEntry
{
    /// <summary>The FHIR Patient.Id of the patient, always present: an event it cannot be resolved for is not produced.</summary>
    public string FhirPatientId { get; set; } = string.Empty;

    /// <summary>The MRN, read from the facility's configured location (PID-3 with type MR by default).</summary>
    public string? MedicalRecordNumber { get; set; }

    /// <summary>The encounter id, read from the facility's configured location (PV1-19 by default).</summary>
    public string? EncounterId { get; set; }

    /// <summary>MSH-7, date/time of message.</summary>
    public DateTime? MessageDateTime { get; set; }

    /// <summary>The trigger event from MSH-9, such as A01.</summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>PID-2, patient id.</summary>
    public string? PatientId { get; set; }

    /// <summary>PID-3, patient identifier list.</summary>
    public List<AdtPatientIdentifier> PatientIdentifierList { get; set; } = [];

    /// <summary>PID-4, alternate patient id.</summary>
    public string? AlternatePatientId { get; set; }

    /// <summary>PID-18, patient account number.</summary>
    public string? PatientAccountNumber { get; set; }

    /// <summary>PV1-2, patient class (HL7 table 0004).</summary>
    public string? PatientClass { get; set; }

    /// <summary>PV1-3, assigned patient location, as sent (point of care^room^bed...).</summary>
    public string? Location { get; set; }

    /// <summary>PV1-44, admit date/time.</summary>
    public DateTime? AdmitDateTime { get; set; }

    /// <summary>PV1-45, discharge date/time.</summary>
    public DateTime? DischargeDateTime { get; set; }

    /// <summary>The MRG segment. Only set for A18 events.</summary>
    public AdtMergeInformation? Merge { get; set; }
}

/// <summary>
/// One repetition of a CX identifier list, such as PID-3 or MRG-1.
/// </summary>
public class AdtPatientIdentifier
{
    /// <summary>CX.1, the identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>CX.4, the assigning authority.</summary>
    public string? AssigningAuthority { get; set; }

    /// <summary>CX.5, the identifier type code, such as MR.</summary>
    public string? IdentifierType { get; set; }
}

/// <summary>
/// The MRG segment of a merge event. Prior patient name (MRG-7) is left out on purpose: nothing consumes it.
/// </summary>
public class AdtMergeInformation
{
    /// <summary>MRG-1, prior patient identifier list.</summary>
    public List<AdtPatientIdentifier> PriorPatientIdentifierList { get; set; } = [];

    /// <summary>MRG-2, prior alternate patient id.</summary>
    public string? PriorAlternatePatientId { get; set; }

    /// <summary>MRG-3, prior patient account number.</summary>
    public string? PriorPatientAccountNumber { get; set; }

    /// <summary>MRG-4, prior patient id.</summary>
    public string? PriorPatientId { get; set; }

    /// <summary>MRG-5, prior visit number.</summary>
    public string? PriorVisitNumber { get; set; }

    /// <summary>MRG-6, prior alternate visit id.</summary>
    public string? PriorAlternateVisitId { get; set; }
}
