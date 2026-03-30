namespace LantanaGroup.Link.Automation.Generation;

/// <summary>
/// Per-patient profile that drives measure-aware generation.
/// Combines a <see cref="MeasureEligibility"/> with an optional seed override
/// so callers get deterministic, repeatable output for any mix of qualifying
/// and non-qualifying patients.
/// </summary>
/// <param name="Eligibility">Whether this patient should qualify for the measure.</param>
/// <param name="SeedOffset">
/// Optional per-patient seed offset. When null the generator assigns one
/// automatically from the patient's ordinal position (same as <c>Generate()</c>).
/// </param>
public record PatientProfile(
    MeasureEligibility Eligibility,
    int? SeedOffset = null);
