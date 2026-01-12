namespace LantanaGroup.Link.Report.Domain.Enums
{
    public enum ReportingStatus
    {
        PatientIdentified,
        //TODO: EvaluatingPatient
        NoReportableReports,
        PendingValidation,
        PassedValidation,
        FailedValidation
    }
}