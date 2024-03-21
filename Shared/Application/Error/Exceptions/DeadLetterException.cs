using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Shared.Application.Error.Exceptions
{
    public class DeadLetterException : Exception
    {
        public AuditEventType AuditEventType { get; set; }

        public DeadLetterException(string message, AuditEventType auditEventType) : base(message)
        {
            AuditEventType = auditEventType;
        }

        public DeadLetterException(string message, AuditEventType auditEventType, Exception? innerEx) : base(message, innerEx)
        {
            AuditEventType = auditEventType;
        }
    }
}
