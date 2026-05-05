using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class CoverageFactory
{
    private static readonly (string Name, string TypeCode, string TypeDisplay)[] Payors =
    [
        ("Medicare",  "PUBLICPOL", "Public Health Insurance Policy"),
        ("Medicaid",  "PUBLICPOL", "Public Health Insurance Policy"),
        ("BlueCross", "EHCPOL",    "Extended Healthcare Policy"),
        ("Aetna",     "EHCPOL",    "Extended Healthcare Policy"),
    ];

    /// <summary>Generate a Coverage from the payor pool using a seed index.</summary>
    public static Coverage Generate(string id, string patientId, DateTime start, DateTime end, int seed)
    {
        var p = Payors[seed % Payors.Length];
        return Create(id, patientId, start, end, p.TypeCode, p.TypeDisplay, p.Name, seed);
    }

    /// <summary>Create a Coverage with caller-supplied payor values.</summary>
    public static Coverage Create(
        string id,
        string patientId,
        DateTime start,
        DateTime end,
        string typeCode,
        string typeDisplay,
        string payorName,
        int seed) => new()
        {
            Id = id,
            Status = FinancialResourceStatusCodes.Active,
            Type = CC("http://terminology.hl7.org/CodeSystem/v3-ActCode", typeCode, typeDisplay),
            SubscriberId = $"SUB-{id.GetStableHash32() & 0xFFFFFF:X6}",
            Beneficiary = Ref($"Patient/{patientId}"),
            Relationship = CC("http://terminology.hl7.org/CodeSystem/subscriber-relationship", "self", "Self"),
            Period = new Period { Start = start.ToString("yyyy-MM-dd"), End = end.AddYears(1).ToString("yyyy-MM-dd") },
            Payor = [Ref($"Patient/{patientId}")],
            Class =
        [
            new Coverage.ClassComponent
            {
                Type  = CC("http://terminology.hl7.org/CodeSystem/coverage-class", "plan", "Plan"),
                Value = $"{typeCode}-PLAN-{seed % 5 + 1:D3}",
                Name  = $"{payorName} Plan {seed % 5 + 1}"
            }
        ]
        };
}
