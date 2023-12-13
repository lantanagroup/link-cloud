using LantanaGroup.Link.Shared.Models;

namespace LantanaGroup.Link.Account.Domain.Messages
{

    public class PropertyChangeModel
    {
        public string PropertyName { get; set; } = string.Empty;
        public string InitialPropertyValue { get; set; } = string.Empty;
        public string NewPropertyValue { get; set; } = string.Empty;
    }


    public class AuditEventMessage
    {
        public string? FacilityId { get; set; }
        public string? ServiceName { get; set; }
        public DateTime? EventDate { get; set; }
        public Guid? UserId { get; set; }
        public string? User { get; set; }
        public AuditEventType? Action { get; set; }
        public string? Resource { get; set; }
        public List<PropertyChangeModel>? PropertyChanges { get; set; }
        public string? Notes { get; set; }
    }


}
