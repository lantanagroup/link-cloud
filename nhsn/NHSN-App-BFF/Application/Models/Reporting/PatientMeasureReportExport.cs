namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

// One patient's measure-report data and evaluated-resource references for a single report type,
// projected from Report's per-patient detail and resource operations, for the patient report
// download action.
public sealed record PatientMeasureReportExport
{
    public required string PatientId { get; init; }

    public required string ReportType { get; init; }

    public string? MeasureReportId { get; init; }

    public required string ReportingStatus { get; init; }

    /// <summary>The report schedule's window, or null when Report has no schedule for this report id.</summary>
    public DateTime? PeriodStart { get; init; }

    /// <inheritdoc cref="PeriodStart"/>
    public DateTime? PeriodEnd { get; init; }

    public IReadOnlyDictionary<string, int> ResourceCountsByType { get; init; } = new Dictionary<string, int>();

    public IReadOnlyList<EvaluatedResourceReference> EvaluatedResources { get; init; } = [];
}

public sealed record EvaluatedResourceReference
{
    public required string ResourceType { get; init; }

    public required string ResourceId { get; init; }
}
