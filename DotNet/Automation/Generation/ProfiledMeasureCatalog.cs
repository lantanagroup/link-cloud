namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Central catalog for measure metadata used by automation (bundle location,
/// display name, and future profile strategy routing).
/// </summary>
public static class ProfiledMeasureCatalog
{
    public static string GetDisplayName(ProfiledMeasureType measure) => measure switch
    {
        ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation => "NHSN Acute Care Hospital Monthly Initial Population",
        ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation => "NHSN Acute Care Hospital Daily Initial Population",
        ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation => "NHSN Glycemic Control Hypoglycemic Initial Population",
        _ => throw new ArgumentOutOfRangeException(nameof(measure), measure, null)
    };

    public static string GetBundleLocation(ProfiledMeasureType measure) => measure switch
    {
        ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation
            => "resource://LantanaGroup.Automation.measures.NHSNAcuteCareHospitalMonthlyInitialPopulation.json",
        ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation
            => "resource://LantanaGroup.Automation.measures.NHSNAcuteCareHospitalDailyInitialPopulation.json",
        ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
            => "resource://LantanaGroup.Automation.measures.NHSNGlycemicControlHypoglycemicInitialPopulation.json",
        _ => throw new ArgumentOutOfRangeException(nameof(measure), measure, null)
    };
}
