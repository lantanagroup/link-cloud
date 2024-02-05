using LantanaGroup.Link.Audit.Domain.Entities;
using static LantanaGroup.Link.Audit.Settings.AuditConstants;

namespace LantanaGroup.Link.Audit.Infrastructure.Logging
{
    public static partial class Logging
    {
        //Microsoft.Extensions.Telemetry
        //Microsoft.Extensions.Compliance.Redaction
        [LoggerMessage(AuditLoggingIds.GenerateItems, LogLevel.Information, "New audit event created")]
        public static partial void LogAuditEventCreation(this ILogger logger, [LogProperties]AuditEntity auditEvent);
    }
}
