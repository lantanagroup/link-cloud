using LantanaGroup.Link.PatientFlowSimulator.Planning;
using LantanaGroup.Link.PatientFlowSimulator.Scenario;

namespace LantanaGroup.Link.PatientFlowSimulator.Generation;

/// <summary>
/// Builds the six census lists Link reads per facility (Admit and Discharge, each split into less than
/// 24 hours, 24 to 48 hours and more than 48 hours ago). Link's Census service admits every patient on
/// an Admit list, discharges every patient on a Discharge list, and discharges anyone it has admitted
/// who is on no list at all, so every admitted patient must stay on exactly one Admit list until
/// discharge.
/// </summary>
public static class CensusListBuilder
{
    /// <summary>
    /// How long a discharged patient stays on the discharge lists.
    /// </summary>
    public static readonly TimeSpan TrailingWindow = TimeSpan.FromHours(72);

    /// <summary>
    /// The six list kinds, in the order Link's facility setup names them.
    /// </summary>
    public static readonly IReadOnlyList<(CensusListStatus Status, CensusTimeFrame TimeFrame)> ListKinds =
    [
        (CensusListStatus.Admit, CensusTimeFrame.LessThan24Hours),
        (CensusListStatus.Admit, CensusTimeFrame.Between24To48Hours),
        (CensusListStatus.Admit, CensusTimeFrame.MoreThan48Hours),
        (CensusListStatus.Discharge, CensusTimeFrame.LessThan24Hours),
        (CensusListStatus.Discharge, CensusTimeFrame.Between24To48Hours),
        (CensusListStatus.Discharge, CensusTimeFrame.MoreThan48Hours)
    ];

    /// <summary>
    /// FHIR id of one census list. Same shape as the Automation facility setup uses.
    /// </summary>
    public static string ListId(string facilityId, CensusListStatus status, CensusTimeFrame timeFrame)
    {
        var statusText = status == CensusListStatus.Admit ? "admit" : "discharge";
        var timeFrameText = timeFrame switch
        {
            CensusTimeFrame.LessThan24Hours => "lt24",
            CensusTimeFrame.Between24To48Hours => "24to48",
            _ => "gt48"
        };

        return $"census-{facilityId}-{statusText}-{timeFrameText}";
    }

    /// <summary>
    /// All six list ids of a facility.
    /// </summary>
    public static IReadOnlyList<string> ListIds(string facilityId)
        => ListKinds.Select(k => ListId(facilityId, k.Status, k.TimeFrame)).ToList();

    /// <summary>
    /// The six lists as they stand at the given instant.
    /// </summary>
    public static IReadOnlyList<CensusListSnapshot> Build(FacilityDefinition facility,
                                                          IEnumerable<PatientStay> stays,
                                                          DateTimeOffset asOfUtc)
    {
        var buckets = ListKinds.ToDictionary(k => k, _ => new List<CensusListEntry>());

        foreach (var stay in stays)
        {
            if (stay.AdmitUtc > asOfUtc)
            {
                continue;
            }

            var entry = new CensusListEntry(stay.PatientId, stay.Identity.DisplayName);
            if (asOfUtc < stay.DischargeUtc)
            {
                buckets[(CensusListStatus.Admit, TimeFrameFor(asOfUtc - stay.AdmitUtc))].Add(entry);
                continue;
            }

            var sinceDischarge = asOfUtc - stay.DischargeUtc;
            if (sinceDischarge < TrailingWindow)
            {
                buckets[(CensusListStatus.Discharge, TimeFrameFor(sinceDischarge))].Add(entry);
            }
        }

        return ListKinds
            .Select(k => new CensusListSnapshot(
                facility.FacilityId,
                k.Status,
                k.TimeFrame,
                ListId(facility.FacilityId, k.Status, k.TimeFrame),
                buckets[k].OrderBy(e => e.PatientId, StringComparer.Ordinal).ToList()))
            .ToList();
    }

    private static CensusTimeFrame TimeFrameFor(TimeSpan since)
    {
        if (since < TimeSpan.FromHours(24))
        {
            return CensusTimeFrame.LessThan24Hours;
        }

        if (since < TimeSpan.FromHours(48))
        {
            return CensusTimeFrame.Between24To48Hours;
        }

        return CensusTimeFrame.MoreThan48Hours;
    }
}
