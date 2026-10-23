using LantanaGroup.Link.PatientFlowSimulator.Generation;
using LantanaGroup.Link.PatientFlowSimulator.Planning;

namespace LantanaGroup.Link.PatientFlowSimulator.Reporting;

/// <summary>
/// One stretch of a stay during which the patient sits on the same census list.
/// </summary>
public sealed record CensusPhase(string Label, DateTimeOffset StartUtc, DateTimeOffset EndUtc)
{
    /// <summary>
    /// The phases of a stay in order: three admit lists while in, three discharge lists afterwards.
    /// Phases the stay is too short to reach are left out.
    /// </summary>
    public static IReadOnlyList<CensusPhase> For(PatientStay stay)
    {
        var day = TimeSpan.FromHours(24);
        var candidates = new[]
        {
            new CensusPhase("Admit list under 24h", stay.AdmitUtc, Min(stay.AdmitUtc + day, stay.DischargeUtc)),
            new CensusPhase("Admit list 24 to 48h", stay.AdmitUtc + day, Min(stay.AdmitUtc + day + day, stay.DischargeUtc)),
            new CensusPhase("Admit list over 48h", stay.AdmitUtc + day + day, stay.DischargeUtc),
            new CensusPhase("Discharge list under 24h", stay.DischargeUtc, stay.DischargeUtc + day),
            new CensusPhase("Discharge list 24 to 48h", stay.DischargeUtc + day, stay.DischargeUtc + day + day),
            new CensusPhase("Discharge list over 48h", stay.DischargeUtc + day + day, stay.DischargeUtc + CensusListBuilder.TrailingWindow)
        };

        return candidates.Where(p => p.EndUtc > p.StartUtc).ToList();
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
}
