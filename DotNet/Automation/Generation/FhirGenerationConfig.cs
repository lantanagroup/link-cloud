namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Configuration for the <see cref="FhirBundleGenerator"/> that controls
/// which resource types are generated and in what proportions.
///
/// <para>
/// <see cref="ResourceDistribution"/> maps resource type names to the fraction
/// of <c>totalResourcesPerPatient</c> to generate for that type. Types not
/// present in the dictionary are skipped entirely, so callers can limit
/// generation to only the resource types their system processes.
/// </para>
///
/// <para>
/// Shared infrastructure resources (Patient, Encounter, Device, Location,
/// Organization, Practitioner, Medication, CareTeam anchor, CarePlan anchor)
/// are always generated regardless of this distribution — they form the
/// clinical anchors that bulk resources reference.
/// </para>
/// </summary>
public class FhirGenerationConfig
{
    /// <summary>
    /// Resource type → fraction of <c>totalResourcesPerPatient</c>.
    /// Fractions do not need to sum to 1.0 — each entry independently
    /// controls how many of that type are created.
    /// </summary>
    public Dictionary<string, double> ResourceDistribution { get; set; } = new Dictionary<string, double>
    {
        ["Observation"] = 0.28,
        ["Condition"] = 0.08,
        ["Procedure"] = 0.07,
        ["MedicationRequest"] = 0.07,
        ["MedicationAdministration"] = 0.09,
        ["DiagnosticReport"] = 0.06,
        ["ServiceRequest"] = 0.07,
        ["Coverage"] = 0.01,
        ["Specimen"] = 0.06,
        ["AllergyIntolerance"] = 0.02,
        ["Immunization"] = 0.03,
        ["ImagingStudy"] = 0.02,
        ["CareTeam"] = 0.01,
        ["CarePlan"] = 0.01,
        ["DocumentReference"] = 0.02,
        ["Provenance"] = 0.02,
    };

    /// <summary>
    /// Controls optional cross-resource links that are not required for core
    /// patient/encounter queryability (e.g., Provenance.target, ImagingStudy.basedOn).
    /// </summary>
    public bool IncludeLowValueOptionalReferences { get; set; } = true;

}
