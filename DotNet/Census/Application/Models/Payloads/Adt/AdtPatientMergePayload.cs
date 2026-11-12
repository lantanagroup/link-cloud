using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Merge patient information: PID describes the surviving patient and MRG the patient merged into it. The surviving
/// Patient.Id, MRN and identifiers are applied with the details common to every message. The surviving MRN replaces the
/// prior one, while other prior identifiers stay on the encounter. Nothing else about the encounter changes.
/// <para>
/// A18 is the original merge event. HL7 replaced it with A40, and with A34, A36, A39 and A30 for older variants, which all
/// mean the same thing to Census.
/// </para>
/// </summary>
public abstract class AdtPatientMergePayload : AdtPayload
{
    public override bool ImpliesAdmission => false;

    public AdtMergeInformation? Merge { get; set; }

    protected AdtPatientMergePayload()
    {
    }

    protected AdtPatientMergePayload(AdtEventEntry entry) : base(entry)
    {
        Merge = entry.Merge;
    }

    /// <summary>
    /// The identifiers that can point at encounters of the patient merged away.
    /// </summary>
    public IReadOnlyCollection<string> GetPriorIdentifiers() => AdtMergeIdentifiers.PriorPatientIdentifiers(Merge);

    /// <summary>
    /// A merge can match several stays, including ended ones, but PV1 describes at most one visit of the surviving
    /// patient, so it must not be applied to all of them.
    /// </summary>
    protected override void ApplyVisitDetails(PatientEncounter patientEncounter)
    {
    }

    protected override void ApplyToEncounter(PatientEncounter patientEncounter)
    {
    }
}
