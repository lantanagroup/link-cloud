namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

// One patient's row in the Report Details patient table, projected from Report's per-entry
// mapping indicators and measure-report resource counts.
public sealed record ReportPatientEntry
{
    public required string PatientId { get; init; }

    // PatientIdentified, NotReportable, PendingValidation, PassedValidation or FailedValidation --
    // Link's ReportingStatus vocabulary. The UI maps this to its own status-pill categories.
    public required string ReportingStatus { get; init; }

    public int ResourceCount { get; init; }

    public IReadOnlyDictionary<string, int> ResourceCountsByType { get; init; } = new Dictionary<string, int>();

    public bool LocationOrgMapped { get; init; }

    public bool EncounterMapped { get; init; }

    public bool HslocMapped { get; init; }

    // Set by the reporting service, not the Report gateway -- this comes from Validation, a
    // different downstream service.
    public bool HasPreQualResults { get; init; }

    // One entry per report type (dQM) this patient was evaluated against. A report requesting
    // several dQMs can carry a different resource count -- and a different measure-report
    // status -- per dQM, even though ReportingStatus above is a single value for the whole
    // patient. This is what lets the Report Details view scope its population and resource
    // counts to whichever dQM tab is active.
    public IReadOnlyList<PatientMeasureReport> MeasureReports { get; init; } = [];
}

public sealed record PatientMeasureReport
{
    public required string ReportType { get; init; }

    public int ResourceCount { get; init; }

    public IReadOnlyDictionary<string, int> ResourceCountsByType { get; init; } = new Dictionary<string, int>();
}
