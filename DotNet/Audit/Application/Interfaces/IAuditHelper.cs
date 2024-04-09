using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Audit.Application.Interfaces
{
    public interface IAuditHelper
    {
        string GetEventTypeName(AuditEventType eventType);
    }
}
