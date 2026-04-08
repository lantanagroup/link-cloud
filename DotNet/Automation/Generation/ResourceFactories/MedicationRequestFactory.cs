using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class MedicationRequestFactory
{
    /// <summary>Generate a MedicationRequest from the pool using a seed index.</summary>
    public static MedicationRequest Generate(
        string id, string patientId, string encounterId,
        DateTime authored, int seed, string practId,
        List<string> conditionIds,
        List<string> medicationIds)
    {
        var v = FhirGenerationCodes.Medications[seed % FhirGenerationCodes.Medications.Length];
        var reasonConditionId = conditionIds.Count > 0
            ? conditionIds[seed % conditionIds.Count]
            : null;
        var medicationRefId = medicationIds.Count > 0
            ? medicationIds[seed % medicationIds.Count]
            : null;

        return Create(id, patientId, encounterId, authored, seed, practId,
                      v.RxCode, v.Display, v.RouteCode, v.RouteDisplay,
                      v.DoseValue, v.DoseUnit, v.FreqPerDay, v.Prn,
                      v.IndicationSnomed, v.IndicationDisplay,
                      reasonConditionId,
                      medicationRefId);
    }

    /// <summary>Create a MedicationRequest with caller-supplied values.</summary>
    public static MedicationRequest Create(
        string id,
        string patientId,
        string encounterId,
        DateTime authored,
        int seed,
        string practId,
        string rxCode,
        string display,
        string routeCode,
        string routeDisplay,
        double doseValue,
        string doseUnit,
        int freqPerDay,
        bool prn,
        string indicationCode,
        string indicationDisplay,
        string? reasonConditionId = null,
        string? medicationRefId = null)
    {
        DataType medicationChoice = !string.IsNullOrWhiteSpace(medicationRefId)
            ? Ref($"Medication/{medicationRefId}", display)
            : RxNorm(rxCode, display);

        var request = new MedicationRequest
        {
            Id = id,
            Status = seed % 5 == 0 ? MedicationRequest.MedicationrequestStatus.Completed
                                        : MedicationRequest.MedicationrequestStatus.Active,
            Intent = MedicationRequest.MedicationRequestIntent.Order,
            Medication = medicationChoice,
            Subject = Ref($"Patient/{patientId}"),
            Encounter = Ref($"Encounter/{encounterId}"),
            AuthoredOn = authored.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Requester = Ref($"Practitioner/{practId}", "Ordering Physician"),
            ReasonCode = [Snomed(indicationCode, indicationDisplay)],
            Note = [new Annotation { Text = new Markdown($"Ordered for {indicationDisplay}. Monitor response.") }],
            DosageInstruction =
            [
                new Dosage
                {
                    Text   = prn
                        ? $"{doseValue} {doseUnit} {routeDisplay} every {24 / freqPerDay}h as needed for {indicationDisplay}"
                        : $"{doseValue} {doseUnit} {routeDisplay} {freqPerDay}x daily",
                    Timing = new Timing
                    {
                        Repeat = new Timing.RepeatComponent
                        {
                            Frequency  = freqPerDay,
                            Period     = 1,
                            PeriodUnit = Timing.UnitsOfTime.D,
                            Bounds     = new Duration { Value = 7, Unit = "d", System = "http://unitsofmeasure.org", Code = "d" }
                        }
                    },
                    AsNeeded = new FhirBoolean(prn),
                    Route    = Snomed(routeCode, routeDisplay),
                    DoseAndRate =
                    [
                        new Dosage.DoseAndRateComponent
                        {
                            Type = CC("http://terminology.hl7.org/CodeSystem/dose-rate-type", "ordered", "Ordered"),
                            Dose = Qty(doseValue, doseUnit)
                        }
                    ]
                }
            ]
        };

        if (reasonConditionId != null)
            request.ReasonReference = [Ref($"Condition/{reasonConditionId}")];

        return request;
    }
}
