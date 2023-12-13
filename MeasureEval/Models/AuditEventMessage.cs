using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.MeasureEval.Models
{
    public class AuditEventMessage
    {
        public string? Id { get; set; }
        public DateTime? EventDate { get; set; }
        public string? UserId { get; set; }
        public string? User { get; set; }
        public string? ServiceName { get; set; }
        public AuditEventType? Action { get; set; }
        public string? Resource { get; set; }
        public string? url { get; set; }
        public string? Notes { get; set; }
    }
}
