using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class MedicationFactory
{
    /// <summary>Generate a Medication from the pool using a seed index, tracking produced IDs.</summary>
    public static Medication Generate(string id, int seed, List<string> medicationIds)
    {
        var v = FhirGenerationCodes.Medications[seed % FhirGenerationCodes.Medications.Length];
        medicationIds.Add(id);
        return Create(id, v.RxCode, v.Display, v.DoseValue, v.DoseUnit, v.RouteCode, v.RouteDisplay);
    }

    /// <summary>Create a Medication with caller-supplied values.</summary>
    public static Medication Create(
        string id, string rxCode, string display,
        double doseValue, string doseUnit,
        string routeCode, string routeDisplay)
    {
        var isTablet = doseUnit == "mg" && routeCode == "26643006";
        var formCode = isTablet ? "385055001" : doseUnit == "[iU]" ? "385218009" : "385219001";
        var formDisplay = isTablet ? "Tablet" : doseUnit == "[iU]" ? "Injection solution" : "Injectable solution";

        return new Medication
        {
            Id = id,
            Code = RxNorm(rxCode, display),
            Status = Medication.MedicationStatusCodes.Active,
            Form = CC("http://snomed.info/sct", formCode, formDisplay),
            Ingredient =
            [
                new Medication.IngredientComponent
                {
                    Item     = RxNorm(rxCode, display),
                    IsActive = true,
                    Strength = new Ratio
                    {
                        Numerator   = Qty(doseValue, doseUnit),
                        Denominator = new Quantity { Value = 1, Unit = "1", System = "http://unitsofmeasure.org", Code = "1" }
                    }
                }
            ]
        };
    }
}
