using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Integration.Report;

public class ReportScheduleApiModel
{
    public Guid Id { get; set; }
    public DateTime? CreateDate { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public Frequency Frequency { get; set; }
    public List<string> ReportTypes { get; set; } = [];
    public DateTime ReportStartDate { get; set; }
    public DateTime ReportEndDate { get; set; }
    public DateTime? SubmitReportDateTime { get; set; }
    public ScheduleStatus Status { get; set; }
    public AdHocType? AdHocType { get; set; }
    public bool EndOfReportPeriodJobHasRun { get; set; }
    public bool EnableSubmission { get; set; }
    public string? PayloadRootUri { get; set; }
    public bool? IsDeleted { get; set; }
}

public class ReportEntryApiModel
{
    public Guid Id { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public ReportingStatus ReportingStatus { get; set; }
    public SubmissionStatus? SubmissionStatus { get; set; }
    public List<EntryMeasureReportApiModel> MeasureReports { get; set; } = [];

    /// <summary>
    /// Whether the patient's encounters resolved to locations belonging to the reporting organization.
    /// </summary>
    public MappingIndicatorStatus LocationOrgStatus { get; set; }

    /// <summary>
    /// Whether the patient's encounters carried locations that could be resolved at all -- a different
    /// question from whether those locations belong to the organization.
    /// </summary>
    public MappingIndicatorStatus EncounterMappingStatus { get; set; }

    /// <summary>
    /// Whether the patient's location type codes were mapped into HSLOC.
    /// </summary>
    public MappingIndicatorStatus HslocMappingStatus { get; set; }

    /// <summary>
    /// When DataAcquisition evaluated the two columns it owns, or null if it has not reported.
    /// </summary>
    /// <remarks>
    /// The two timestamps separate "this source reported nothing to say" from "this source has not
    /// answered yet". Without them a zero and an absence look identical.
    /// </remarks>
    public DateTime? AcquisitionEvaluatedAt { get; set; }

    /// <summary>
    /// When Normalization evaluated the code maps, or null if it has not reported.
    /// </summary>
    /// <inheritdoc cref="AcquisitionEvaluatedAt" path="/remarks"/>
    public DateTime? NormalizationEvaluatedAt { get; set; }
}

/// <summary>
/// One entry with the evidence behind its mapping indicators, as returned by the per-patient operation.
/// </summary>
/// <remarks>
/// The paged search returns <see cref="ReportEntryApiModel"/> and carries no evidence: serializing it for
/// every row of every page is work a table view never reads. Call the per-patient operation for the
/// detail behind a single indicator.
/// </remarks>
public class ReportEntryDetailApiModel : ReportEntryApiModel
{
    /// <summary>
    /// What DataAcquisition found when it resolved the patient's encounters, or null if it never reported.
    /// </summary>
    /// <remarks>
    /// Null rather than an empty object on purpose: an empty object would claim the source ran and found
    /// nothing, which is a different fact from its not having answered.
    /// </remarks>
    public AcquisitionMappingDetailsApiModel? Acquisition { get; set; }

    /// <summary>
    /// What Normalization counted per code map, or null if it never reported.
    /// </summary>
    /// <inheritdoc cref="Acquisition" path="/remarks"/>
    public NormalizationMappingDetailsApiModel? Normalization { get; set; }
}

public class AcquisitionMappingDetailsApiModel
{
    public LocationOrgDetailsApiModel LocationOrg { get; set; } = new();
}

public class LocationOrgDetailsApiModel
{
    public int EncounterCount { get; set; }

    /// <summary>Encounters resolved to a location belonging to the organization.</summary>
    public int OrgEncounterCount { get; set; }

    /// <summary>
    /// Of those, how many were treated as belonging to the organization without being verified, because
    /// the encounter carried no resolvable location reference.
    /// </summary>
    public int AssumedOrgEncounterCount { get; set; }

    public List<LocationOrgMatchApiModel> Matches { get; set; } = [];
}

public class LocationOrgMatchApiModel
{
    public string LocationId { get; set; } = string.Empty;
    public string? LocationName { get; set; }
    public string? LocationAlias { get; set; }
    public string? PartOfValue { get; set; }
    public bool IsOrgLocation { get; set; }
}

public class NormalizationMappingDetailsApiModel
{
    /// <summary>The combined totals across every acquisition pass.</summary>
    public List<CodeMapOutcomeApiModel> CodeMaps { get; set; } = [];

    /// <summary>Each pass's own contribution, from which the totals above are summed.</summary>
    public List<NormalizationPassApiModel> Passes { get; set; } = [];
}

public class NormalizationPassApiModel
{
    public string? CorrelationId { get; set; }
    public string? QueryType { get; set; }
    public List<CodeMapOutcomeApiModel> CodeMaps { get; set; } = [];
}

public class CodeMapOutcomeApiModel
{
    public string SourceSystem { get; set; } = string.Empty;
    public string TargetSystem { get; set; } = string.Empty;
    public int MappedCount { get; set; }
    public int UnmappedCount { get; set; }
    public int FailureCount { get; set; }

    /// <summary>
    /// The distinct source codes that had no entry in the map -- what an operator would go and configure.
    /// Capped; <see cref="UnmappedCount"/> is the true total.
    /// </summary>
    public List<string> UnmappedCodes { get; set; } = [];
}

/// <summary>
/// The value behind one of the report detail mapping indicators.
/// </summary>
/// <remarks>
/// Declared here rather than shared with the Report service's own enum, matching how
/// <see cref="ReportingStatus"/> and <see cref="SubmissionStatus"/> are handled: the SDK contract is
/// deliberately decoupled, so a service-internal refactor cannot silently change what consumers see.
/// <c>MappingIndicatorStatusContractTests</c> asserts the two stay identical.
/// </remarks>
public enum MappingIndicatorStatus
{
    /// <summary>No source has reported for this entry yet.</summary>
    NotEvaluated = 0,

    /// <summary>Nothing is configured to produce this value.</summary>
    NotApplicable = 1,

    /// <summary>Everything that could be mapped was mapped.</summary>
    Mapped = 2,

    /// <summary>Some mapped and some did not; the per-patient detail names which.</summary>
    PartiallyMapped = 3,

    /// <summary>Nothing mapped, though there was something to map.</summary>
    Unmapped = 4,

    /// <summary>The mapping could not be determined because the operation itself failed.</summary>
    Unknown = 5,

    /// <summary>
    /// Treated as belonging to the reporting organization without being verified, because the encounters
    /// carried no resolvable location references.
    /// </summary>
    Assumed = 6,

    /// <summary>Configured correctly, but no resource reached it to be mapped.</summary>
    NothingToEvaluate = 7,

    /// <summary>
    /// The patient is not in this report -- none of their encounters belonged to the reporting
    /// organization -- so no mapping result about them is meaningful for it.
    /// </summary>
    Excluded = 8
}

public class ReportEntrySummaryApiModel
{
    public Dictionary<string, int> ReportTypeCounts { get; set; } = [];
    public Dictionary<string, int> ReportingStatusCounts { get; set; } = [];
    public Dictionary<string, int> SubmissionStatusCounts { get; set; } = [];
}

public enum ReportingStatus
{
    PatientIdentified,
    NotReportable,
    PendingValidation,
    PassedValidation,
    FailedValidation
}

public enum SubmissionStatus
{
    PendingValidation,
    Submitting,
    Submitted,
    FailedSubmission,
    NotEligable
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReportStatus
{
    Unknown,
    Canceled,
    Completed,
    Pending
}

public class EntryMeasureReportApiModel
{
    public string? MeasureReportId { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public EntryMeasureReportStatus? Status { get; set; }
    public Dictionary<string, int> ResourceCount { get; set; } = [];
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EntryMeasureReportStatus
{
    EntryCreated = 0,
    NotReportable = 1,
    ReadyForValidation = 2
}

public class ReportResourceApiModel
{
    public Guid Id { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public Guid ReportScheduleId { get; set; }
    public string PatientId { get; set; } = string.Empty;
    public string MeasureReportId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
}

public class ReportPopulationApiModel
{
    public Guid Id { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public Guid ReportScheduleId { get; set; }
    public string? Measure { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public List<GroupPopulationApiModel> GroupPopulations { get; set; } = [];
}

public class GroupPopulationApiModel
{
    public string PopulationId { get; set; } = string.Empty;
    public string? PopulationCodeJson { get; set; }
    public int TotalPopulationCount { get; set; }
    public List<MeasureReportPopulationApiModel> MeasureReportPopulations { get; set; } = [];
}

public class MeasureReportPopulationApiModel
{
    public int Id { get; set; }
    public int GroupPopulationId { get; set; }
    public string MeasureReportId { get; set; } = string.Empty;
    public int PopulationCount { get; set; }
}

public class ReportSummaryApiModel
{
    public string ReportScheduleId { get; set; } = string.Empty;
    public string FacilityId { get; set; } = string.Empty;
    public DateTimeOffset ReportStartDate { get; set; }
    public DateTimeOffset ReportEndDate { get; set; }
    public List<string> ReportTypes { get; set; } = [];
    public ReportStatus Status { get; set; }
    public int PatientCount { get; set; }
    public int InitialPopulationCount { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
}