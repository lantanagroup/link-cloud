namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Identifies a measure that can be selected for automation runs and used as
/// a context for profile-driven patient generation.
/// </summary>
public enum ProfiledMeasureType
{
    /// <summary>
    /// NHSN Acute Care Hospital Monthly Initial Population.
    /// </summary>
    NhsnAcuteCareHospitalMonthlyInitialPopulation,

    /// <summary>
    /// NHSN Acute Care Hospital Daily Initial Population.
    /// </summary>
    NhsnAcuteCareHospitalDailyInitialPopulation,

    /// <summary>
    /// NHSN Glycemic Control Hypoglycemic Initial Population.
    /// </summary>
    NhsnGlycemicControlHypoglycemicInitialPopulation
}
