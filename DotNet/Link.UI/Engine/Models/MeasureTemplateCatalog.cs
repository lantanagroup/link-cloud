using LantanaGroup.Automation.Generation;

namespace Automation.UI.Models;

/// <summary>
/// Stable system measure-template ids and helpers to map the closed
/// <see cref="ProfiledMeasureType"/> generation families onto those rows.
/// </summary>
public static class MeasureTemplateCatalog
{
    public static readonly Guid AchMonthlyId = new("00000000-0000-0000-2000-000000000001");
    public static readonly Guid AchDailyId = new("00000000-0000-0000-2000-000000000002");
    public static readonly Guid HypoglycemicId = new("00000000-0000-0000-2000-000000000003");

    public static IReadOnlyList<(Guid Id, ProfiledMeasureType Family)> SystemTemplates { get; } =
    [
        (AchMonthlyId, ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation),
        (AchDailyId, ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation),
        (HypoglycemicId, ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation)
    ];

    public static Guid SystemIdFor(ProfiledMeasureType family) => family switch
    {
        ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation => AchMonthlyId,
        ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation => AchDailyId,
        ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation => HypoglycemicId,
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, null)
    };

    public static List<Guid> SystemIdsFor(IEnumerable<ProfiledMeasureType> families) =>
        families.Select(SystemIdFor).Distinct().ToList();
}
