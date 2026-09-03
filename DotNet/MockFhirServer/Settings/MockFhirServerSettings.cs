namespace LantanaGroup.Link.MockFhirServer.Settings;

public class MockFhirServerSettings
{
    public const string ConfigSectionName = "MockFhirServer";

    /// <summary>
    /// Number of patients to generate at startup. These will be available immediately
    /// without any generation delay when queried by DataAcquisition.
    /// </summary>
    public int PreGeneratedPatientCount { get; set; } = 10;

    /// <summary>
    /// Number of FHIR resources to generate per patient (distributed across resource types
    /// according to <see cref="LantanaGroup.Automation.Generation.FhirGenerationConfig.ResourceDistribution"/>).
    /// </summary>
    public int ResourcesPerPatient { get; set; } = 100;

    /// <summary>
    /// Optional fixed seed for the pre-generated patients. When null, each startup
    /// produces a different set of pre-generated patient IDs. Set this for reproducible
    /// test runs that need deterministic patient IDs.
    /// </summary>
    public int? GenerationSeed { get; set; }

    /// <summary>
    /// Optional ISO 8601 start of the clinical period used to bound encounter dates.
    /// When provided with <see cref="ClinicalPeriodEnd"/>, generated encounter windows
    /// will fall entirely within this range so date-filtered FHIR queries return results.
    /// Example: "2024-01-01T00:00:00Z"
    /// </summary>
    public string? ClinicalPeriodStart { get; set; }

    /// <summary>
    /// Optional ISO 8601 end of the clinical period. See <see cref="ClinicalPeriodStart"/>.
    /// Example: "2024-12-31T23:59:59Z"
    /// </summary>
    public string? ClinicalPeriodEnd { get; set; }

}
