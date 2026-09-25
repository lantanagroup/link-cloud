namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

// What POST /reports accepts. No facility -- it comes from the token.
public sealed record ReportRequest
{
    // Digital quality measures, as served by GET /reference/measures.
    public IReadOnlyList<string> Measures { get; init; } = [];

    // Date-only, ISO 8601 (yyyy-MM-dd), facility-local.
    public string? StartDate { get; init; }

    public string? EndDate { get; init; }

    public IReadOnlyList<string> PatientIds { get; init; } = [];
}
