namespace LantanaGroup.Link.Shared.Application.Models.Telemetry
{
    public static class DiagnosticNames
    {
        //Diagnostic tag names
        public const string StackTrace = "stack.trace";
        public const string Service = "service";
        public const string CorrelationId = "correlation.id";
        public const string EntityId = "entity.id";
        public const string DataAcquisitionLogId = "log.id";
        public const string ReportTrackingId = "report.tracking.id";
        public const string FacilityId = "facility.id";
        public const string PatientId = "patient.id";
        public const string PatientEvent = "patient.event";
        public const string Phase = "phase";
        public const string Resource = "resource";
        public const string ResourceType = "resource.type";
        public const string ResourceId = "resource.id";
        public const string AuditLogAction = "audit.log.action";
        public const string ReportType = "report.type";
        public const string PeriodStart = "period.start";
        public const string PeriodEnd = "period.end";
        public const string UserId = "user.id";
        public const string Email = "email";
        public const string Role = "role";
        public const string Duration = "duration";
        public const string OperationType = "operation.type";
        public const string DestinationType = "destination.type";
        public const string RetryAttempts = "retry.attempts";
        public const string Outcome = "outcome";
        public const string GroupKind = "group.kind";
        public const string Cache = "cache";
        public const string From = "from";
        public const string To = "to";

        //Diagnostic tags Searching
        public const string SearchParameters = "search.parameters";
        public const string FacilityFilter = "facility.filter";
        public const string CorrelationFilter = "correlation.filter";
        public const string ServiceFilter = "service.filter";
        public const string ActionFilter = "action.filter";
        public const string UserFilter = "user.filter";
        public const string SortBy = "sort.by";
        public const string SortOrder = "sort.order";
        public const string PageNumber = "page.number";
        public const string PageSize = "page.size";

        //Diagnostic activity names
        public const string CreateAuditEvent = "Create Audit Event";
        
        //Diagnostic metric names
        public const string DataAcquisitionQueryDuration = "link_data_acq_query_duration";
        public const string DataAcquisitionResourceAcquiredCount = "link_data_acq_resource_acquired_count";
        public const string DataAcquisitionSemaphoreWaitDuration = "link_data_acq_semaphore_wait_duration";
        public const string NormalizationResourceChangedCount = "link_normalization_resource_changed_count";
        public const string NormalizationResourceNotChangedCount = "link_normalization_resource_not_changed_count";
        public const string NormalizationDuration = "link_normalization_duration";
        public const string ReportStatusTransitionCount = "link_report_status_transition_count";
        public const string ReportPersistDuration = "link_report_persist_duration";
        public const string SubmissionUploadCount = "link_submission_upload_count";
        public const string TerminologyLookupCount = "link_terminology_lookup_count";
        public const string TerminologyLookupDuration = "link_terminology_lookup_duration";

        public static string NormalizePhase(string? phase)
        {
            if (string.IsNullOrEmpty(phase) || phase.Length < 2) return phase;
            return phase.Substring(0, 1).ToUpperInvariant() + phase.Substring(1).ToLowerInvariant();
        }
    }
}
