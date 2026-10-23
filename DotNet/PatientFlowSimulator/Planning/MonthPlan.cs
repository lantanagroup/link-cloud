using LantanaGroup.Link.PatientFlowSimulator.Scenario;

namespace LantanaGroup.Link.PatientFlowSimulator.Planning;

/// <summary>
/// Every stay that matters to one month: the ones admitted in it and the ones carried over from
/// earlier months.
/// </summary>
public sealed class MonthPlan
{
    public MonthPlan(YearMonth month, IReadOnlyList<PatientStay> stays, IReadOnlyList<PatientStay> carryOvers)
    {
        Month = month;
        Stays = stays;
        CarryOvers = carryOvers;
    }

    /// <summary>
    /// The month.
    /// </summary>
    public YearMonth Month { get; }

    /// <summary>
    /// Stays admitted in this month.
    /// </summary>
    public IReadOnlyList<PatientStay> Stays { get; }

    /// <summary>
    /// Stays admitted in an earlier month that are still in, or were discharged recently enough to be on
    /// a discharge list when this month starts.
    /// </summary>
    public IReadOnlyList<PatientStay> CarryOvers { get; }

    /// <summary>
    /// Every stay, own and carried over, for one facility.
    /// </summary>
    public IReadOnlyList<PatientStay> StaysFor(FacilityDefinition facility)
        => Stays
            .Concat(CarryOvers)
            .Where(s => string.Equals(s.Facility.FacilityId, facility.FacilityId, StringComparison.OrdinalIgnoreCase))
            .ToList();
}
