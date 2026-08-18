using Hl7.Fhir.Model;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

/// <summary>
/// Lightweight factory for FHIR POCO concept helpers shared by all resource factories.
/// </summary>
public static class FhirConceptFactory
{
    public static ResourceReference Ref(string reference, string? display = null)
    {
        var r = new ResourceReference(reference);
        if (display != null) r.Display = display;
        return r;
    }

    public static Quantity Qty(double value, string unit) => new()
    {
        Value = (decimal)value,
        Unit = unit,
        System = "http://unitsofmeasure.org",
        Code = unit
    };

    public static CodeableConcept CC(string system, string code, string? display = null) => new()
    {
        Coding = [new Coding(system, code, display)],
        Text = display
    };

    public static CodeableConcept Loinc(string code, string display) =>
        CC("http://loinc.org", code, display);

    public static CodeableConcept Snomed(string code, string display) =>
        CC("http://snomed.info/sct", code, display);

    public static CodeableConcept RxNorm(string code, string display) =>
        CC("http://www.nlm.nih.gov/research/umls/rxnorm", code, display);
}
