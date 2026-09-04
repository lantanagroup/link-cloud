namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Derived initial-population outcome for a measure, predicted from the clinical
/// shape (or classified from imported FHIR). Not a generation switch.
/// </summary>
public enum MeasureEligibility
{
    /// <summary>
    /// Clinical shape is predicted to satisfy the measure's Initial Population.
    /// </summary>
    Qualifying,

    /// <summary>
    /// Clinical shape is predicted not to satisfy the measure's Initial Population.
    /// </summary>
    NonQualifying
}
