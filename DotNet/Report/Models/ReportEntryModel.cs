using LantanaGroup.Link.Report.Domain.Enums;

namespace LantanaGroup.Link.Report.Models;
public class ReportEntryModel
{
    public Guid Id { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public Guid ReportScheduleId { get; set; }
    public string PatientId { get; set; } = string.Empty;
    public ReportingStatus ReportingStatus { get; set; }
    public SubmissionStatus? SubmissionStatus { get; set; }
    public DateTime? SubmitReportDateTime { get; set; }
    public string AggregateReportUri { get; set; } = string.Empty;
    public string AggregateReportBlobName { get; set; } = string.Empty;

    public List<EntryMeasureReportModel> MeasureReports { get; set; } = new();

    /// <summary>
    /// Whether the patient's encounters were resolved to locations belonging to the reporting
    /// organization.
    /// </summary>
    /// <remarks>
    /// <see cref="MappingIndicatorStatus.Assumed"/> is the one worth reading twice: acquisition treats an
    /// encounter carrying no resolvable location reference as belonging to the organization, so membership
    /// was never actually verified against the facility's configuration.
    /// </remarks>
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
    /// The two timestamps are what separate "this source said nothing to report" from "this source has not
    /// answered yet". Without them a zero and an absence look the same.
    /// </remarks>
    public DateTime? AcquisitionEvaluatedAt { get; set; }

    /// <summary>
    /// When Normalization evaluated the code maps, or null if it has not reported.
    /// </summary>
    /// <inheritdoc cref="AcquisitionEvaluatedAt" path="/remarks"/>
    public DateTime? NormalizationEvaluatedAt { get; set; }
}

public class EntryMeasureReportModel
{
    public string? MeasureReportId { get; set; }
    public MeasureReportStatus Status { get; set; } = MeasureReportStatus.EntryCreated;
    public string ReportType { get; set; } = string.Empty;
    public string? MeasureReportUri { get; set; }
    public string? MeasureReportFileName { get; set; }
    public Dictionary<string, int> ResourceCount { get; set; } = new();
}