namespace LantanaGroup.Link.PatientFlowSimulator.Generation;

/// <summary>
/// What a generated month contains. Written next to the hourly files so the roster and the expected
/// measure counts can be checked against Link without re-running the generator.
/// </summary>
public sealed class MonthManifest
{
    public string Month { get; set; } = "";
    public string Scenario { get; set; } = "";
    public string Seed { get; set; } = "";
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public int LookbackMonths { get; set; }
    public int FileCount { get; set; }
    public int EntryCount { get; set; }
    public string? FirstFile { get; set; }
    public string? LastFile { get; set; }
    public List<FacilityManifest> Facilities { get; set; } = [];
}
