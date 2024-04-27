namespace LantanaGroup.Link.Shared.Application.Models.Telemetry
{
    public static class DiagnosticNames
    {
        //Diagnostic tag names
        public const string Service = "service";
        public const string CorrelationId = "correlation.id";
        public const string FacilityId = "facility.id";
        public const string PatientId = "patient.id";
        public const string PatientEvent = "patient.event";
        public const string QueryType = "query.type";
        public const string Resource = "resource";
        public const string NormalizationOperation = "normalization.operation";
        public const string AuditLogAction = "audit.log.action";
        public const string NotificationId = "notification.id";
        public const string NotificationType = "notification.type";
        public const string NotificationChannel = "notification.channel";
        public const string RecipientCount = "recipient.count";
        public const string ReportType = "report.type";
        public const string PeriodStart = "period.start";
        public const string PeriodEnd = "period.end";
        public const string PageSize = "page.size";

        //Diagnostic activity names
        public const string CreateAuditEvent = "Create Audit Event";
    }
}
