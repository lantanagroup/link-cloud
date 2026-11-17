namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

// The report section of FacilityDraft, in our vocabulary. Owned by Report — the BFF persists no
// copy of schedule data, only the in-flight request state (patientIds, lastRequestedReportId) that
// belongs to the onboarding wizard itself.
public sealed record ReportScheduleSummary
{
    /// The most recently created schedule's id.
    public required string ReportId { get; init; }

    public IReadOnlyList<string> Measures { get; init; } = [];
}
