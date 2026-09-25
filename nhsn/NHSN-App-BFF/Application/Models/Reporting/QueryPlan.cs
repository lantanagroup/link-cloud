namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

// DataAcquisition's per-vendor query plan. Its shape isn't part of the Link SDK's typed surface
// (the client returns raw JSON), so it passes through as-is rather than being re-modeled here.
public sealed record QueryPlan
{
    public required string ReportId { get; init; }

    public required string PlanJson { get; init; }
}

// One FHIR query DataAcquisition ran (or is about to run) while acquiring a report -- one row
// per DataAcquisitionLogApiModel.FhirQuery entry, which is what the "View Acquisition Log" /
// "Export Report Summary" queries table in the onboarding POC shows. There is no acquisition
// timeline/timestamp in this shape -- DataAcquisition only records CompletionDate once a log
// finishes, which the POC's own log view never showed either.
public sealed record AcquisitionLogEntry
{
    public required string PatientId { get; init; }

    /// <summary>The FHIR resource type(s) this query acquired, e.g. "Encounter".</summary>
    public required string Resource { get; init; }

    /// <summary>"Initial" or "Supplemental".</summary>
    public required string QueryPhase { get; init; }

    /// <summary>"Read", "Search", "SearchPost", "BulkDataRequest" or "BulkDataPoll".</summary>
    public string? QueryType { get; init; }

    public IReadOnlyList<string> Parameters { get; init; } = [];

    public required string Status { get; init; }
}

// DataAcquisition's own summary counts for a report -- distinct from Report's ReportSummaryApiModel,
// which covers a different service's view of the same report.
public sealed record AcquisitionReportSummary
{
    public required string ReportId { get; init; }

    public int TotalLogs { get; init; }

    public int TotalPatients { get; init; }

    public int TotalCompletedPatients { get; init; }

    public int TotalResourcesAcquired { get; init; }

    public IReadOnlyList<AcquisitionStatusCount> StatusCounts { get; init; } = [];

    public IReadOnlyList<AcquisitionResourceTypeCount> ResourceTypeCounts { get; init; } = [];
}

public sealed record AcquisitionStatusCount
{
    public required string Status { get; init; }

    public int Count { get; init; }
}

public sealed record AcquisitionResourceTypeCount
{
    public required string ResourceType { get; init; }

    public int Count { get; init; }
}
