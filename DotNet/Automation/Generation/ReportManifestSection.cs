namespace LantanaGroup.Automation.Generation;

/// <summary>
/// The saved report side of a manifest, keyed by report id. Built once from the Report service
/// (automatically at the end of an Automation run, or the first time a real report's Manifest tab
/// opens) so the manifest still loads after the facility's service data is cleaned up.
/// Links are kept as ids and resolved when the page renders.
/// </summary>
public sealed class ReportManifestSection
{
    public string ReportId { get; init; } = string.Empty;
    public string FacilityId { get; init; } = string.Empty;
    public string? FacilityName { get; init; }

    /// <summary>The Automation run that produced the report, when there is one.</summary>
    public Guid? RunId { get; init; }

    public DateTimeOffset BuiltAt { get; init; }
    public int PatientCount { get; init; }
    public int InitialPopulation { get; init; }
    public IReadOnlyList<string> Measures { get; init; } = [];
    public IReadOnlyList<ReportManifestPopulation> Populations { get; init; } = [];
    public int? PassedValidation { get; init; }
    public int? FailedValidation { get; init; }
    public int? PendingValidation { get; init; }
    public IReadOnlyDictionary<string, int> StatusCounts { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> ResourceTypeCounts { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<ReportManifestPatient> Patients { get; init; } = [];
}

public sealed class ReportManifestPopulation
{
    public string Measure { get; init; } = string.Empty;
    public string PopulationId { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class ReportManifestPatient
{
    public string PatientId { get; init; } = string.Empty;
    public string? ReportingStatus { get; init; }
    public string? SubmissionStatus { get; init; }
    public Dictionary<string, int> ResourceCounts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> MeasureReportIds { get; init; } = [];

    /// <summary>Population memberships as "measure|populationId", resolved once when the manifest is built.</summary>
    public List<string> Populations { get; init; } = [];

    public List<ReportManifestTimelineEntry> Timeline { get; init; } = [];
    public DateTime? Updated { get; init; }
    public string? LocationOrgStatus { get; init; }
    public string? EncounterMappingStatus { get; init; }
    public string? HslocMappingStatus { get; init; }
}

public sealed class ReportManifestTimelineEntry
{
    public string Label { get; init; } = string.Empty;
    public DateTime At { get; init; }
}