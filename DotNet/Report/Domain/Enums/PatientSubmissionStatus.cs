namespace LantanaGroup.Link.Report.Domain.Enums;

public enum PatientSubmissionStatus
{
    PendingEvaluation = 1,
    NotReportable = 2,
    ReadyForValidation = 3,
    ValidationRequested = 4,
    ValidationComplete = 5,
    Submitted = 6
}

