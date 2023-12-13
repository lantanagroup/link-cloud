namespace LantanaGroup.Link.Shared.Application.Models;

public enum KafkaTopic
{
    DataAcquired,
    PatientIDsAcquired,
    PatientAcquired,
    DataAcquisitionScheduled,
    DataAcquisitionRequested,
    DataAcquisitionFailed,
    PatientDataEvaluated,
    PatientNormalized,
    PatientDischarged,
    PatientDataAcquired,
    ReportBundled,
    ReportFailed,
    ReportRequestRejected,
    ReportScheduled,
    RetentionCheckScheduled,
    PatientResourcesNormalized,
    MeasureChanged,
    MeasureEvalFailed,
    FHIRValidationFailed,
    AuditableEventOccurred,
    NotificationRequested,
    PatientCensusScheduled,
    PatientEvent,
    MeasureEvaluated,
    ReportSubmitted,
    BundleEvalRequested,
    PatientsToQuery,
    SubmitReport
}
