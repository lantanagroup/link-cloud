namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

// A validated ad hoc report request, ready for Tenant. Dates are resolved to instants here rather
// than in the adapter, so the string parsing has one home.
public sealed record AdHocReportCommand
{
    public required string FacilityId { get; init; }

    // Digital quality measures, distinct -- Tenant refuses a request naming one twice.
    public required IReadOnlyList<string> ReportTypes { get; init; }

    public required DateTime StartDate { get; init; }

    public required DateTime EndDate { get; init; }

    public IReadOnlyList<string> PatientIds { get; init; } = [];
}
