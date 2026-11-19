using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Hl7;

/// <summary>
/// Reads the elements Link consumes out of an ADT message. FHIR Patient.Id resolution is left to the caller.
/// </summary>
public static class AdtEventMapper
{
    private static readonly Hl7Path MessageDateTime = Hl7Path.Parse("MSH-7.1");
    private static readonly Hl7Path MessageControlId = Hl7Path.Parse("MSH-10");
    private static readonly Hl7Path TriggerEvent = Hl7Path.Parse("MSH-9.2");
    private static readonly Hl7Path EventTypeCode = Hl7Path.Parse("EVN-1");
    private static readonly Hl7Path PatientId = Hl7Path.Parse("PID-2.1");
    private static readonly Hl7Path AlternatePatientId = Hl7Path.Parse("PID-4.1");
    private static readonly Hl7Path PatientAccountNumber = Hl7Path.Parse("PID-18.1");
    private static readonly Hl7Path PatientClass = Hl7Path.Parse("PV1-2.1");
    private static readonly Hl7Path AdmitDateTime = Hl7Path.Parse("PV1-44.1");
    private static readonly Hl7Path DischargeDateTime = Hl7Path.Parse("PV1-45.1");
    private static readonly Hl7Path PriorAlternatePatientId = Hl7Path.Parse("MRG-2.1");
    private static readonly Hl7Path PriorPatientAccountNumber = Hl7Path.Parse("MRG-3.1");
    private static readonly Hl7Path PriorPatientId = Hl7Path.Parse("MRG-4.1");
    private static readonly Hl7Path PriorVisitNumber = Hl7Path.Parse("MRG-5.1");
    private static readonly Hl7Path PriorAlternateVisitId = Hl7Path.Parse("MRG-6.1");

    /// <summary>
    /// Events whose MRG segment says what the patient or visit was known as before: patient merges, and visit merges,
    /// moves and number changes.
    /// </summary>
    private static readonly HashSet<string> EventsWithMrg = ["A18", "A30", "A34", "A36", "A39", "A40", "A42", "A45", "A50"];

    /// <summary>
    /// The message control id (MSH-10), for identifying a message in logs without any patient data.
    /// </summary>
    public static string? GetMessageControlId(Hl7Message message) => message.GetValue(MessageControlId);

    /// <summary>
    /// The trigger event, such as A01: MSH-9.2, or EVN-1 for older messages that only carry it there.
    /// </summary>
    public static string? GetMessageType(Hl7Message message) =>
        (message.GetValue(TriggerEvent) ?? message.GetValue(EventTypeCode))?.ToUpperInvariant();

    /// <summary>
    /// Splits a message that can carry several merges into one message per merge, each mapped to its own entry. A39, A40
    /// and A42 repeat a PID/MRG group, one per patient or visit merged, and A45 repeats MRG/PV1 for each visit moved.
    /// Every other message is returned as it is.
    /// </summary>
    public static IReadOnlyList<Hl7Message> SplitIntoEvents(Hl7Message message)
    {
        var groupStart = GetMessageType(message) switch
        {
            "A39" or "A40" or "A42" => "PID",
            "A45" => "MRG",
            _ => null
        };

        return groupStart is null ? [message] : message.SplitGroups(groupStart);
    }

    /// <summary>
    /// Maps a message to an entry. <see cref="AdtEventEntry.FhirPatientId"/> is left for the caller to set.
    /// </summary>
    public static AdtEventEntry Map(Hl7Message message, string messageType, Hl7Path mrnLocation, Hl7Path encounterIdLocation)
    {
        return new AdtEventEntry
        {
            MedicalRecordNumber = message.GetValue(mrnLocation),
            EncounterId = message.GetValue(encounterIdLocation),
            MessageDateTime = Hl7Message.ParseTimestamp(message.GetValue(MessageDateTime)),
            MessageType = messageType,
            PatientId = message.GetValue(PatientId),
            PatientIdentifierList = GetIdentifiers(message, "PID", 3),
            AlternatePatientId = message.GetValue(AlternatePatientId),
            PatientAccountNumber = message.GetValue(PatientAccountNumber),
            PatientClass = message.GetValue(PatientClass),
            AdmitDateTime = Hl7Message.ParseTimestamp(message.GetValue(AdmitDateTime)),
            DischargeDateTime = Hl7Message.ParseTimestamp(message.GetValue(DischargeDateTime)),
            Merge = EventsWithMrg.Contains(messageType) && message.HasSegment("MRG")
                ? new AdtMergeInformation
                {
                    PriorPatientIdentifierList = GetIdentifiers(message, "MRG", 1),
                    PriorAlternatePatientId = message.GetValue(PriorAlternatePatientId),
                    PriorPatientAccountNumber = message.GetValue(PriorPatientAccountNumber),
                    PriorPatientId = message.GetValue(PriorPatientId),
                    PriorVisitNumber = message.GetValue(PriorVisitNumber),
                    PriorAlternateVisitId = message.GetValue(PriorAlternateVisitId),
                }
                : null,
        };
    }

    private static List<AdtPatientIdentifier> GetIdentifiers(Hl7Message message, string segment, int field) =>
        message.GetIdentifierList(segment, field)
            .Select(i => new AdtPatientIdentifier { Id = i.Id, AssigningAuthority = i.AssigningAuthority, IdentifierType = i.IdentifierType })
            .ToList();
}
