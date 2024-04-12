using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Shared.Application.Error.Exceptions
{
    public class TransientException : Exception
    {
        public AuditEventType AuditEventType { get; set; }

        public TransientException(string message, AuditEventType auditEventType) : base(message)
        {
            AuditEventType = auditEventType;
        }

        public TransientException(string message, AuditEventType auditEventType, Exception? innerEx) : base(message, innerEx)
        {
            AuditEventType = auditEventType;
        }
    }
}
