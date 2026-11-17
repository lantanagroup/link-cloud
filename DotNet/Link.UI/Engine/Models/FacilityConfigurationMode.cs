namespace Automation.UI.Models;

/// <summary>
/// How a scenario gets its facility-level pieces.
/// <see cref="Unspecified"/> is the legacy document shape: query plan, normalization,
/// and organization resource map fall back to the system defaults.
/// </summary>
public enum FacilityConfigurationMode
{
    Unspecified = 0,
    Facility = 1,
    AlaCarte = 2
}
