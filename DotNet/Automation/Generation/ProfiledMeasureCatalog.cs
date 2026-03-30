namespace LantanaGroup.Link.Automation.Generation;

/// <summary>
/// Central catalog for measure metadata used by automation (bundle location,
/// display name, and future profile strategy routing).
/// </summary>
public static class ProfiledMeasureCatalog
{
    public static string GetDisplayName(ProfiledMeasureType measure) => measure switch
    {
        ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation => "NHSN Acute Care Hospital Monthly Initial Population",
        _ => throw new ArgumentOutOfRangeException(nameof(measure), measure, null)
    };

    public static string GetBundleLocation(ProfiledMeasureType measure) => measure switch
    {
        ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation
            => "resource://LantanaGroup.Link.Automation.measures.NHSNAcuteCareHospitalMonthlyInitialPopulation.json",
        _ => throw new ArgumentOutOfRangeException(nameof(measure), measure, null)
    };
}
