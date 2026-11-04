using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Merge patient information: PID describes the surviving patient and MRG the patient merged into it. The surviving
/// Patient.Id, MRN and identifiers are applied with the details common to every message, while the prior identifiers
/// stay on the encounter.
/// </summary>
public class AdtA18Payload : AdtPayload
{
    public override EventType EventType => EventType.A18;
    public override bool ImpliesAdmission => false;

    public AdtMergeInformation? Merge { get; set; }

    public AdtA18Payload()
    {
    }

    public AdtA18Payload(AdtEventEntry entry) : base(entry)
    {
        Merge = entry.Merge;
    }

    /// <summary>
    /// The identifiers that can point at encounters of the patient merged away.
    /// </summary>
    public IReadOnlyCollection<string> GetPriorIdentifiers()
    {
        if (Merge is null)
        {
            return [];
        }

        return Merge.PriorPatientIdentifierList.Select(x => x.Id)
            .Append(Merge.PriorPatientId)
            .Append(Merge.PriorAlternatePatientId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct()
            .ToList();
    }

    protected override void ApplyToEncounter(PatientEncounter patientEncounter)
    {
    }
}
