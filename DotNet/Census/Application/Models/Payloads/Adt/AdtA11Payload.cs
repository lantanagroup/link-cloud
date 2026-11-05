using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Cancel admit/visit: the encounter is kept, marked cancelled and closed at its admit date so it no longer counts as admitted.
/// </summary>
public class AdtA11Payload : AdtPayload
{
    public override EventType EventType => EventType.A11;
    public override bool ImpliesAdmission => false;

    public AdtA11Payload()
    {
    }

    public AdtA11Payload(AdtEventEntry entry) : base(entry)
    {
    }

    protected override void ApplyToEncounter(PatientEncounter patientEncounter)
    {
        patientEncounter.DischargeDate = patientEncounter.AdmitDate;
    }
}
