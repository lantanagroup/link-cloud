using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Notification.Application.Models
{
    public class AuditEventMessage
    {
        public string? FacilityId { get; set; }
        public string? ServiceName { get; set; }
        public string? CorrelationId { get; set; }
        public DateTime? EventDate { get; set; }
        public string? UserId { get; set; }
        public string? User { get; set; }
        public AuditEventType? Action { get; set; }
        public string? Resource { get; set; }
        public List<PropertyChangeModel>? PropertyChanges { get; set; }
        public string? Notes { get; set; }
    }
}
