namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Controls whether a generated patient should qualify for the measure's Initial Population.
/// </summary>
public enum MeasureEligibility
{
    /// <summary>
    /// Generated to satisfy the selected measure's Initial Population criteria.
    /// </summary>
    Qualifying,

    /// <summary>
    /// Generated to avoid the selected measure's Initial Population criteria.
    /// </summary>
    NonQualifying
}
