using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Audit.Infrastructure.AuditHelper
{
    public class AuditHelper : IAuditHelper
    {
        public string GetEventTypeName(AuditEventType eventType)
        {
            switch (eventType)
            {
                case AuditEventType.Create:
                    {
                        return "Create";
                    }
                case AuditEventType.Update:
                    {
                        return "Update";
                    }
                case AuditEventType.Delete:
                    {
                        return "Delete";
                    }
                case AuditEventType.Query:
                    {
                        return "Query";
                    }
                case AuditEventType.Submit:
                    {
                        return "Submit";
                    }
                default:
                    {
                        return "Unknown";
                    }
            }
        }
    }
}
