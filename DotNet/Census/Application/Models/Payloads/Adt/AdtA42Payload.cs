using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Merge visit: the patient's visit in MRG-5 is merged into the visit in the message's encounter id, which is then known
/// by both numbers, so a late message with either reaches it.
/// <list type="bullet">
/// <item>When Census tracks only the prior visit, that encounter becomes the surviving visit and gains its number.</item>
/// <item>When it also tracks the surviving visit, the two were one stay counted twice. The surviving encounter gains the
/// prior numbers and is otherwise left as it is. The prior one gets an event of its own, with
/// <see cref="MergedIntoAnotherEncounter"/> set: it is closed at its admit date and loses its visit numbers, so no later
/// message matches it.</item>
/// </list>
/// </summary>
public class AdtA42Payload : AdtVisitChangePayload
{
    public override EventType EventType => EventType.A42;

    /// <summary>
    /// Whether this is the prior encounter's event when the surviving visit was already tracked as an encounter of its own.
    /// It is stored so that a rebuild, which replays one encounter at a time, reaches the same result.
    /// </summary>
    public bool MergedIntoAnotherEncounter { get; set; }

    public AdtA42Payload()
    {
    }

    public AdtA42Payload(AdtEventEntry entry) : base(entry)
    {
    }

    /// <summary>
    /// The payload to record on the prior encounter when the surviving visit is a different encounter.
    /// </summary>
    public AdtA42Payload ForMergedAwayEncounter()
    {
        var payload = (AdtA42Payload)MemberwiseClone();
        payload.MergedIntoAnotherEncounter = true;
        return payload;
    }

    protected override void ApplyVisitDetails(PatientEncounter patientEncounter)
    {
        if (MergedIntoAnotherEncounter)
        {
            RemoveVisitIdentifiers(patientEncounter, patientEncounter.PatientVisitIdentifiers.Select(x => x.Identifier).ToList());
            return;
        }

        foreach (var priorVisitId in AdtMergeIdentifiers.PriorVisitIdentifiers(Merge))
        {
            AddVisitIdentifierIfMissing(patientEncounter, priorVisitId);
        }

        AddVisitIdentifierIfMissing(patientEncounter, EncounterId);
    }

    protected override void ApplyToEncounter(PatientEncounter patientEncounter)
    {
        if (MergedIntoAnotherEncounter)
        {
            patientEncounter.DischargeDate = patientEncounter.AdmitDate;
        }
    }
}
