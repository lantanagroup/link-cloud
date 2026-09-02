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
    public DateTime? CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public ReportingStatus ReportingStatus { get; set; }
    public SubmissionStatus? SubmissionStatus { get; set; }
    public List<EntryMeasureReportApiModel> MeasureReports { get; set; } = [];
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
