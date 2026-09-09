namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

// One report, as the UI's report list and report request both see it.
public sealed record ReportSummary
{
    public required string ReportId { get; init; }

    public IReadOnlyList<string> Measures { get; init; } = [];

    public int PatientCount { get; init; }

    public string? StartDate { get; init; }

    public string? EndDate { get; init; }

    public required string CreateDate { get; init; }

    // Pending, Complete, Failed or Cancelled -- the UI's vocabulary, not Link's.
    public required string Status { get; init; }
}
