using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Move visit information: a visit registered to the wrong patient moves to the patient in PID. The encounter of the
/// patient in MRG-1 with the visit number in MRG-5, or the message's encounter id when MRG-5 is empty, takes this
/// patient's Patient.Id, MRN and identifiers, and drops the prior patient's: unlike a merge, they belong to someone else.
/// </summary>
public class AdtA45Payload : AdtVisitChangePayload
{
    public override EventType EventType => EventType.A45;

    protected override bool ReplacesPatient => true;

    public AdtA45Payload()
    {
    }

    public AdtA45Payload(AdtEventEntry entry) : base(entry)
    {
    }

    /// <summary>
    /// The identifiers of the patient the visit is moved from.
    /// </summary>
    public IReadOnlyCollection<string> GetPriorIdentifiers() => AdtMergeIdentifiers.PriorPatientIdentifiers(Merge);

    public override IReadOnlyCollection<string> GetVisitIdentifiersToMatch()
    {
        var prior = base.GetVisitIdentifiersToMatch();

        // A move usually keeps the visit number, so senders often leave MRG-5 empty
        return prior.Count > 0 || string.IsNullOrWhiteSpace(EncounterId) ? prior : [EncounterId];
    }
}
