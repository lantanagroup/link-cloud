using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Update patient information: corrects a stay already tracked. It is sent for every kind of visit, at any time, so it
/// never opens a stay. The MRN and identifiers are applied with the details common to every message; the admit date is
/// corrected from PV1-44, and the discharge date from PV1-45 only for a stay already discharged.
/// </summary>
public class AdtA08Payload : AdtPayload
{
    public override EventType EventType => EventType.A08;
    public override bool ImpliesAdmission => false;

    public AdtA08Payload()
    {
    }

    public AdtA08Payload(AdtEventEntry entry) : base(entry)
    {
    }

    protected override void ApplyToEncounter(PatientEncounter patientEncounter)
    {
        patientEncounter.AdmitDate = AdmitDateTime ?? patientEncounter.AdmitDate;

        // Only an A03 discharges, because only it tells downstream about it
        if (patientEncounter.DischargeDate is not null && DischargeDateTime is not null)
        {
            patientEncounter.DischargeDate = DischargeDateTime;
        }
    }
}
