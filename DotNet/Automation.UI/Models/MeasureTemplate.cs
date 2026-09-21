using LantanaGroup.Automation.Generation;

namespace Automation.UI.Models;

/// <summary>
/// A reusable FHIR measure definition used by automation runs.
/// System rows are seeded from embedded bundles and are immutable (clone to customize).
/// Custom rows require a <see cref="GenerationFamily"/> so patient generation
/// stays within what Automation can produce. ABS instance prediction reads this
/// template's <see cref="BundleJson"/> CQL at run time.
/// </summary>
public sealed class MeasureTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Closed set of patient-generation rules Automation can apply.
    /// Instance-level ABS prediction uses <see cref="BundleJson"/> CQL instead.
    /// </summary>
    public ProfiledMeasureType GenerationFamily { get; set; } =
        ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation;

    /// <summary>
    /// FHIR transaction Bundle JSON (Measure + Library + ValueSet + CodeSystem).
    /// </summary>
    public string BundleJson { get; set; } = string.Empty;

    public string? MeasureId { get; set; }
    public string? CanonicalUrl { get; set; }
    public string? Version { get; set; }
    public string? MeasureDate { get; set; }
    public string? Status { get; set; }
}
