namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

// What OnboardingDrafts.DraftJson stores: the workflow-state fields of FacilityDraft, and nothing
// else. No configuration value may be added here — configuration belongs to the Link service that
// owns it and is read back on every GET /onboarding rather than persisted.
//
// The split runs per field, not per section, which is what makes this easy to get wrong. The UI's
// fhir slice carries fhirServerBaseUrl (Data Acquisition's) next to connectionTested (ours); its
// census slice carries sFTP settings (Data Acquisition's) next to accuracyAcknowledged (a BFF
// acknowledgement row). Only the second of each pair appears below.
//
// Also absent, each owned elsewhere: currentStepId (the Facilities column), schemaVersion (its own
// column, read before this can be parsed), unlockedStepIds (its own column).
//
// Mirrors NHSN-App-UI/src/core/onboarding/types.ts by hand — a field added there needs a matching
// change here.
public sealed record OnboardingDraftState
{
    // A drill-down inside a step. Today only Report Details uses one — it keeps its step's id so a
    // reload inside the drill-down returns there rather than to the top of the step.
    public StepView? CurrentView { get; init; }

    public FhirWorkflowState Fhir { get; init; } = new();

    // Contract-pending: HSLOC mappings have no Link owner until Normalization support lands.
    public HslocWorkflowState Hsloc { get; init; } = new();

    public ManualUploadWorkflowState ManualUpload { get; init; } = new();

    public ReportWorkflowState Report { get; init; } = new();

    public ReportResultsWorkflowState ReportResults { get; init; } = new();

    public ReportingPlanWorkflowState ReportingPlan { get; init; } = new();
}

public sealed record StepView
{
    public string StepId { get; init; } = string.Empty;
    public string View { get; init; } = string.Empty;
    public Dictionary<string, string>? Params { get; init; }
}

// Whether step 5's connection probe has been run. The FHIR settings themselves are Data
// Acquisition's.
public sealed record FhirWorkflowState
{
    public bool? ConnectionTested { get; init; }
}

// HSLOC mappings, held here only until Normalization can own them. The code list itself is not
// here — that's reference data proxied from Normalization. Only the facility's mapping choices are
// contract-pending.
public sealed record HslocWorkflowState
{
    public List<HslocMappingState> Mappings { get; init; } = [];
}

public sealed record HslocMappingState
{
    public string? LocationId { get; init; }
    public string? HslocCode { get; init; }
}

public sealed record ManualUploadWorkflowState
{
    public string? UploadedFileName { get; init; }
    public DateTimeOffset? UploadedOn { get; init; }
}

// Report request state. measures, startDate and endDate are absent — those are Tenant's.
public sealed record ReportWorkflowState
{
    // Patient ids for an ad hoc request — a parameter the user is composing, not stored configuration.
    public List<string> PatientIds { get; init; } = [];

    public string? LastRequestedReportId { get; init; }
}

// Which report the user is looking at and its last seen status. accuracyAcknowledged is absent —
// acknowledgements are append-only BFF rows.
public sealed record ReportResultsWorkflowState
{
    public string? ViewingReportId { get; init; }
    public string? LatestStatus { get; init; }
}

public sealed record ReportingPlanWorkflowState
{
    public bool? Reviewed { get; init; }
}
