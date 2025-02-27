using System.Runtime.Serialization;

namespace LantanaGroup.Link.Report.Domain.Enums;

public enum PatientSubmissionStatus
{
    [EnumMember(Value = "NotEvaluated")]
    NotEvaluated = 1,
    [EnumMember(Value = "NotReportable")]
    NotReportable = 2,
    [EnumMember(Value = "ReadyForSubmission")]
    ReadyForSubmission = 3
}

