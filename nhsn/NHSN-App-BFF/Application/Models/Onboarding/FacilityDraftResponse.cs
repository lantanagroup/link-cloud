using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

// What GET /onboarding returns. Mirrors DraftEnvelope in NHSN-App-UI/src/core/api/ApiClient.ts.
public sealed record DraftEnvelopeResponse
{
    public FacilityDraftResponse? Draft { get; init; }

    // The last commit attempt, or null before one has been made.
    public object? CommitState { get; init; }

    // Per-section origin and status. Always present, one entry per section.
    public IReadOnlyList<SectionSource> Sources { get; init; } = [];
}

// The whole onboarding picture, assembled per request from Link, the Facilities row, the draft row
// and the BFF's normalized tables.
//
// Assembled, never stored — the UI sees one object and never learns which value came from where.
// Nothing here should get a persistence attribute; a value that needs saving belongs to its owning
// Link service or a BFF table, not to this type.
public sealed record FacilityDraftResponse
{
    public int SchemaVersion { get; init; }

    // From the Facilities column, not DraftJson.
    public string? CurrentStepId { get; init; }

    public StepView? CurrentView { get; init; }

    public IReadOnlyList<string> UnlockedStepIds { get; init; } = [];

    public FacilityInfoSection FacilityInfo { get; init; } = new();
    public ManualUploadSection ManualUpload { get; init; } = new();
    public FhirSection Fhir { get; init; } = new();
    public CensusSection Census { get; init; } = new();
    public LocationOrgSection LocationOrg { get; init; } = new();
    public HslocSection Hsloc { get; init; } = new();
    public EncounterSection Encounter { get; init; } = new();
    public ReportSection Report { get; init; } = new();
    public ReportResultsSection ReportResults { get; init; } = new();
    public ReportingPlanSection ReportingPlan { get; init; } = new();
}

// Tenant.
public sealed record FacilityInfoSection
{
    public string? TimeZone { get; init; }
    public EhrVendor? Vendor { get; init; }
}

// Workflow state only — the import sheet itself is not stored.
public sealed record ManualUploadSection
{
    public string? UploadedFileName { get; init; }
    public DateTimeOffset? UploadedOn { get; init; }
}

// Data Acquisition, except ConnectionTested which is a UI flag from DraftJson.
public sealed record FhirSection
{
    public string? FhirServerBaseUrl { get; init; }
    public int? MaxConcurrentRequests { get; init; }
    public int? MaxRetries { get; init; }
    public string? MinAcquisitionPullTime { get; init; }
    public string? MaxAcquisitionPullTime { get; init; }

    // Query Dispatch's dispatch schedule, surfaced on this step in the UI.
    public string? LagDuration { get; init; }

    public bool? ConnectionTested { get; init; }
}

// Data Acquisition for the lists and sFTP settings, Census for the frequency, BFF tables for the
// acknowledgement. hasCredentials is read from Data Acquisition's credentials/status endpoint, not
// tracked by the BFF, and the credentials themselves never enter this shape.
public sealed record CensusSection
{
    public IReadOnlyDictionary<string, string> PatientListIds { get; init; } = new Dictionary<string, string>();
    public string? SftpHost { get; init; }
    public int? SftpPort { get; init; }
    public string? SftpRemoteDirectory { get; init; }
    public bool? SftpRemoveAfterProcessing { get; init; }
    public bool? HasCredentials { get; init; }
    public string? AcquisitionFrequency { get; init; }
    public bool? AccuracyAcknowledged { get; init; }
}

// Data Acquisition.
public sealed record LocationOrgSection
{
    public string? Method { get; init; }
    public IReadOnlyList<string> ManagingOrganizationIds { get; init; } = [];

    // Pairs, not bare codes: a type code says what kind of Location it is and the alias says which
    // one, and Data Acquisition needs both halves to resolve a facility. Same for an identifier,
    // which is only meaningful against the system that issued it. Draft schema version 2.
    public IReadOnlyList<LocationTypeEntry> LocationTypes { get; init; } = [];
    public IReadOnlyList<LocationIdentifierEntry> LocationIdentifiers { get; init; } = [];
    public string? CustomFhirPath { get; init; }
}

public sealed record LocationTypeEntry
{
    public string Code { get; init; } = string.Empty;
    public string Alias { get; init; } = string.Empty;
}

public sealed record LocationIdentifierEntry
{
    public string System { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
}

// Contract-pending — held in DraftJson until Normalization owns it.
public sealed record HslocSection
{
    public IReadOnlyList<HslocMappingState> Mappings { get; init; } = [];
}

// Normalization (SearchFacilityOperationsAsync / CreateOperationAsync) — not Data Acquisition.
public sealed record EncounterSection
{
    public IReadOnlyList<string> CodeSystems { get; init; } = [];
    public IReadOnlyList<object> Mappings { get; init; } = [];
}

// Tenant for the measures and period; DraftJson for the request state.
public sealed record ReportSection
{
    public IReadOnlyList<string> Measures { get; init; } = [];
    public string? StartDate { get; init; }
    public string? EndDate { get; init; }
    public IReadOnlyList<string> PatientIds { get; init; } = [];
    public string? LastRequestedReportId { get; init; }
}

// DraftJson, except the acknowledgement which is an append-only BFF row.
public sealed record ReportResultsSection
{
    public string? ViewingReportId { get; init; }
    public bool? AccuracyAcknowledged { get; init; }
    public string? LatestStatus { get; init; }
}

public sealed record ReportingPlanSection
{
    public bool? Reviewed { get; init; }
}
