namespace LantanaGroup.Link.Shared.Application.Models.Telemetry
{
    public static class DiagnosticNames
    {
        //Diagnostic tag names
        public const string Service = "service";
        public const string CorrelationId = "correlation.id";
        public const string ReportId = "report.id";
        public const string FacilityId = "facility.id";
        public const string PatientId = "patient.id";
        public const string PatientEvent = "patient.event";
        public const string QueryType = "query.type";
        public const string Resource = "resource";
        public const string ResourceId = "resource.id";
        public const string NormalizationOperation = "normalization.operation";
        public const string AuditId = "audit.id";
        public const string AuditLogAction = "audit.log.action";
        public const string NotificationId = "notification.id";
        public const string NotificationType = "notification.type";
        public const string NotificationChannel = "notification.channel";
        public const string RecipientCount = "recipient.count";
        public const string ReportType = "report.type";
        public const string PeriodStart = "period.start";
        public const string PeriodEnd = "period.end";        
        public const string UserId = "user.id";
        public const string UserName = "user.name";
        public const string Email = "email";
        public const string Role = "role";
        public const string EncounterClass = "encounter.class";
        public const string EncounterType = "encounter.type";
        public const string LocationType = "location.type";
        public const string DiagnosticReportCode = "diagnostic.report.code";
        public const string MedicationCode = "medication.code";
        public const string MedicationRequestReasonCode = "medication.request.reason.code";
        public const string MedicationRequestCategory = "medicaton.request.category";
        public const string ObservationCode = "observation.code";
        public const string SpecimenType = "specimen.type";
        public const string ServiceRequestCategory = "service.request.category";
        public const string Measures = "report.measures";

        //Diagnostic tags Searching
        public const string SearchText = "search.text";
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
    }
}
