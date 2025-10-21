using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class MedicationAdministrationFactory
{
    /// <summary>Generate a MedicationAdministration from the pool using a seed index.</summary>
    public static MedicationAdministration Generate(
        string id, string patientId, string encounterId,
        DateTime effective, int seed, List<string> medicationIds, string practId)
    {
        var v = FhirGenerationCodes.Medications[seed % FhirGenerationCodes.Medications.Length];
        var medRefId = medicationIds.Count > 0 ? medicationIds[seed % medicationIds.Count] : null;
        // IV infusions get an effective period; oral/subcut get a point-in-time
        var isIv = v.RouteCode == "47625008";
        return Create(id, patientId, encounterId, effective, seed, practId,
                      v.RxCode, v.Display, v.RouteCode, v.RouteDisplay,
                      v.DoseValue, v.DoseUnit, v.IndicationSnomed, v.IndicationDisplay,
                      isIv, medRefId);
    }

    /// <summary>Create a MedicationAdministration with caller-supplied values.</summary>
    public static MedicationAdministration Create(
        string id,
        string patientId,
        string encounterId,
        DateTime effective,
        int seed,
        string practId,
        string rxCode,
        string display,
        string routeCode,
        string routeDisplay,
        double doseValue,
        string doseUnit,
        string indicationCode,
        string indicationDisplay,
        bool infusionPeriod = false,
        string? medicationRefId = null)
    {
        DataType medChoice;
        if (medicationRefId != null)
        {
            var rr = new ResourceReference($"Medication/{medicationRefId}", display);
            rr.Extension.Add(new Extension(
                "https://www.cdc.gov/nhsn/fhir/nhsnlink/StructureDefinition/nhsnlink-original-id",
                new FhirString($"Medication/{medicationRefId}")));
            medChoice = rr;
        }
        else
        {
            medChoice = RxNorm(rxCode, display);
        }

        DataType effectiveElement = infusionPeriod
            ? new Period
            {
                StartElement = new FhirDateTime(effective),
                EndElement = new FhirDateTime(effective.AddHours(1 + seed % 3))
            }
            : new FhirDateTime(effective);

        return new MedicationAdministration
        {
            Id = id,
            Status = MedicationAdministration.MedicationAdministrationStatusCodes.Completed,
            Category = CC("http://terminology.hl7.org/CodeSystem/medication-admin-location", "inpatient", "Inpatient"),
            Medication = medChoice,
            Subject = Ref($"Patient/{patientId}"),
            Context = Ref($"Encounter/{encounterId}"),
            Effective = effectiveElement,
            Performer =
            [
                new MedicationAdministration.PerformerComponent
                {
                    Function = CC("http://terminology.hl7.org/CodeSystem/med-admin-perform-function", "performer", "Performer"),
                    Actor    = Ref($"Practitioner/{practId}", "Administering Nurse")
                }
            ],
            ReasonCode = [Snomed(indicationCode, indicationDisplay)],
            Dosage = new MedicationAdministration.DosageComponent
            {
                Text = $"{doseValue} {doseUnit} {routeDisplay}",
                Route = Snomed(routeCode, routeDisplay),
                Dose = Qty(doseValue, doseUnit)
            }
        };
    }
}
