using System.Reflection;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Display names and system measure-definition JSON for the closed generation families.
/// ACH Daily/Monthly are Validation NHSN 2.0.0-cibuild; Hypoglycemic is the current
/// NHSN hypoglycemic package (no 2.0 file in-repo).
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

    public static string ReadBundleJson(ProfiledMeasureType measure, Assembly? assembly = null)
    {
        var resourceName = measure switch
        {
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation
                => "LantanaGroup.Automation.measures.NHSNAcuteCareHospitalMonthlyInitialPopulation.json",
            ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation
                => "LantanaGroup.Automation.measures.NHSNAcuteCareHospitalDailyInitialPopulation.json",
            ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
                => "LantanaGroup.Automation.measures.NHSNGlycemicControlHypoglycemicInitialPopulation.json",
            _ => throw new ArgumentOutOfRangeException(nameof(measure), measure, null)
        };

        assembly ??= typeof(ProfiledMeasureCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException(
                $"Embedded measure '{resourceName}' was not found in '{assembly.GetName().Name}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
