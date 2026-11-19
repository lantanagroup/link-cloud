using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// A change to how a visit is identified: MRG-5 and MRG-6 give the visit number it was known by, and the message's
/// encounter id the one it is known by now. Without this, the next message for the visit would carry an encounter id
/// Census has never seen, and open a second stay.
/// </summary>
public abstract class AdtVisitChangePayload : AdtPayload
{
    public override bool ImpliesAdmission => false;

    public AdtMergeInformation? Merge { get; set; }

    protected AdtVisitChangePayload()
    {
    }

    protected AdtVisitChangePayload(AdtEventEntry entry) : base(entry)
    {
        Merge = entry.Merge;
    }

    /// <summary>
    /// The visit identifiers that find the encounter this message changes.
    /// </summary>
    public virtual IReadOnlyCollection<string> GetVisitIdentifiersToMatch() => AdtMergeIdentifiers.PriorVisitIdentifiers(Merge);

    /// <summary>
    /// The encounter is now known by the message's encounter id instead of its prior visit numbers.
    /// </summary>
    protected override void ApplyVisitDetails(PatientEncounter patientEncounter)
    {
        if (string.IsNullOrWhiteSpace(EncounterId))
        {
            return;
        }

        RemoveVisitIdentifiers(patientEncounter, AdtMergeIdentifiers.PriorVisitIdentifiers(Merge).Where(x => x != EncounterId).ToList());
        base.ApplyVisitDetails(patientEncounter);
    }

    protected override void ApplyToEncounter(PatientEncounter patientEncounter)
    {
    }
}
