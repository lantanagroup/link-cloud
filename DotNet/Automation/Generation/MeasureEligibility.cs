namespace LantanaGroup.Link.Automation.Generation;

/// <summary>
/// Controls whether a generated patient should qualify for the measure's Initial Population.
/// </summary>
public enum MeasureEligibility
{
    /// <summary>
    /// Inpatient encounter within the measurement period — will be included in the Initial Population.
    /// </summary>
    Qualifying,

    /// <summary>
    /// Ambulatory/outpatient encounter — will NOT be included in the Initial Population.
    /// </summary>
    NonQualifying
}
