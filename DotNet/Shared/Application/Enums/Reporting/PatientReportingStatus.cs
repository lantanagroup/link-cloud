using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Enums.Reporting;

/// <summary>
/// A patient's progress through the reporting lifecycle. In ReportStatusUpdated, the status identifies
/// the stage that completed; Report advances the patient to the next stage on success.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PatientReportingStatus
{
    PatientIdentified,
    InitialAcquisition,
    InitialNormalization,
    InitialEvaluation,
    SupplementalAcquisition,
    SupplementalNormalization,
    SupplementalEvaluation,
    Validation,
    Submission,
    Submitted,
    Failed,
    NotReportable,
    NotSubmitted
}
