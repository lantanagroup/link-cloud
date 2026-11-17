namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

// A single report's full detail view. Extends ReportSummary with the measure -> digital quality
// measure mapping the Report Details page shows.
public sealed record ReportDetail : ReportSummary
{
    // Always empty today: Report has no measure-mapping endpoint yet (DMRP's mapping proposal is
    // still in development), matching the onboarding POC's own note on this field.
    public IReadOnlyList<MeasureMapping> MeasureMapping { get; init; } = [];
}

public sealed record MeasureMapping
{
    public required string NhsnMeasure { get; init; }

    public required string DigitalQualityMeasure { get; init; }
}
