namespace LantanaGroup.Automation.Generation;

/// <summary>
/// One selectable generation-ready code (condition, lab, med, …).
/// Bulk-loaded from measure value sets or seeded from <see cref="FhirGenerationCodes"/>.
/// </summary>
public sealed class GenerationCatalogItem
{
    public const string Snomed = "http://snomed.info/sct";
    public const string Loinc = "http://loinc.org";
    public const string RxNorm = "http://www.nlm.nih.gov/research/umls/rxnorm";
    public const string SpecimenType = "http://terminology.hl7.org/CodeSystem/v2-0488";

    public Guid Id { get; set; } = Guid.NewGuid();
    public GenerationCatalogKind Kind { get; set; }
    public string System { get; set; } = "";
    public string Code { get; set; } = "";
    public string Display { get; set; } = "";
    public string? Category { get; set; }
    public string? Unit { get; set; }
    public double? NormLow { get; set; }
    public double? NormHigh { get; set; }
    public string? IcdCode { get; set; }
    public bool IsLab { get; set; }
    public bool Incomplete { get; set; }
    public string? SourceValueSet { get; set; }
    public string? SourceMeasure { get; set; }
    public bool IsSeed { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string DedupKey => $"{Kind}|{System}|{Code}".ToLowerInvariant();

    public static string GuessSystem(GenerationCatalogKind kind, string? system, string code)
    {
        if (!string.IsNullOrWhiteSpace(system))
            return system.Trim();
        if (global::System.Text.RegularExpressions.Regex.IsMatch(code ?? "", @"^\d{1,5}-\d{1,2}$"))
            return Loinc;
        return kind switch
        {
            GenerationCatalogKind.Observation => Loinc,
            GenerationCatalogKind.Medication => RxNorm,
            GenerationCatalogKind.Specimen => SpecimenType,
            GenerationCatalogKind.ServiceRequest => Loinc,
            _ => Snomed
        };
    }
}
