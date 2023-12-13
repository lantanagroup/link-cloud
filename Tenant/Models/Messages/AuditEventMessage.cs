

using LantanaGroup.Link.Shared.Application.Models;


namespace LantanaGroup.Link.Tenant.Models.Messages
{
    public class AuditEventMessage
    {
        public string? FacilityId { get; set; }
        public string? ServiceName { get; set; }
        public string? CorrelationId { get; set; }
        public DateTime? EventDate { get; set; }
        public Guid? UserId { get; set; }
        public string? User { get; set; }
        public AuditEventType? Action { get; set; }
        public string? Resource { get; set; }
        public List<PropertyChangeModel>? PropertyChanges { get; set; }
        public string? Notes { get; set; }
    }
}
