using System.Reflection;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Central catalog for measure metadata used by automation (bundle location,
/// display name, and embedded CQL used by instance-level ABS prediction).
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

    /// <summary>
    /// Reads the embedded FHIR measure-bundle JSON used by MeasureEval and by
    /// <see cref="CqlFilterSimulator"/> instance prediction.
    /// </summary>
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
