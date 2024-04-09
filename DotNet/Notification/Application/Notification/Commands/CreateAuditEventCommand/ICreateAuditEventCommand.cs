using LantanaGroup.Link.Notification.Application.Models;

namespace LantanaGroup.Link.Notification.Application.Notification.Commands
{
    public interface ICreateAuditEventCommand
    {
        Task<bool> Execute(string? facilityId, AuditEventMessage auditEvent);
    }
}
