using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Settings;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Notification.Application.Factory
{
    public class AuditEventFactory : IAuditEventFactory
    {
        public AuditEventMessage CreateAuditEvent(string? correlationId, string? userId, string? userName, AuditEventType action, string resource, string notes)
        {
            AuditEventMessage auditEvent = new()
            {               
                ServiceName = NotificationConstants.ServiceName,
                CorrelationId = correlationId,
                EventDate = DateTime.UtcNow,
                UserId = userId,
                User = userName,
                Action = action,
                Resource = resource,
                Notes = notes
            };

            return auditEvent;
        }
    }
}
