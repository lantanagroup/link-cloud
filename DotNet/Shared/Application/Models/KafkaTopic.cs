using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.Shared.Application.Models;

public enum KafkaTopic
{
    DataAcquired,
    PatientIDsAcquired,
    PatientAcquired,
    [StringValue("PatientAcquired-Retry")]
    PatientAcquiredRetry,
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
    [StringValue("PatientEvent-Retry")]
    PatientEventRetry,
    MeasureEvaluated,
    ReportSubmitted,
    BundleEvalRequested,
    PatientsToQuery,
    SubmitReport,
    [StringValue("MeasureEvaluated-Retry")]
    MeasureEvaluatedRetry,
    [StringValue("ReportSubmitted-Retry")]
    ReportSubmittedRetry,
    [StringValue("BundleEvalRequested-Retry")]
    BundleEvalRequestedRetry,
    [StringValue("PatientsToQuery-Retry")]
    PatientsToQueryRetry,
    [StringValue("SubmitReport-Retry")]
    SubmitReportRetry,
    [StringValue("ReportScheduled-Retry")]
    ReportScheduledRetry

}
