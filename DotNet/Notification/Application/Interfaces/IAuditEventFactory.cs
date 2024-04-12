using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Notification.Application.Interfaces
{
    public interface IAuditEventFactory
    {
        public AuditEventMessage CreateAuditEvent(string? correlationId, string? userId, string? userName, AuditEventType action, string resource, string notes);
    }
}
