namespace LantanaGroup.Link.PatientFlowSimulator.Generation;

/// <summary>
/// The members of one census list at one instant.
/// </summary>
public sealed record CensusListSnapshot(
    string FacilityId,
    CensusListStatus Status,
    CensusTimeFrame TimeFrame,
    string ListId,
    IReadOnlyList<CensusListEntry> Entries)
{
    /// <summary>
    /// Human-readable list name.
    /// </summary>
    public string Title => $"{(Status == CensusListStatus.Admit ? "Admitted" : "Discharged")} {TimeFrameText}";

    /// <summary>
    /// True when both snapshots list the same patients.
    /// </summary>
    public bool SameEntriesAs(CensusListSnapshot other)
        => other.ListId == ListId && Entries.Select(e => e.PatientId).SequenceEqual(other.Entries.Select(e => e.PatientId));

    private string TimeFrameText => TimeFrame switch
    {
        CensusTimeFrame.LessThan24Hours => "less than 24 hours ago",
        CensusTimeFrame.Between24To48Hours => "24 to 48 hours ago",
        _ => "more than 48 hours ago"
    };
}
