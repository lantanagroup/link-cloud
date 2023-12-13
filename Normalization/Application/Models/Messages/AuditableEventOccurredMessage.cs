using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Normalization.Application.Models.Messages;

public class AuditableEventOccurredMessage
{
    public string FacilityId { get; set; }
    public AuditEvent AuditEvent { get; set; }
    public string CorrelationId { get; set; }
}

public class AuditEvent
{
    public string? ServiceName { get; set; }
    public DateTime? EventDate { get; set; }
    public string? UserId { get; set; }
    public string? User { get; set; }
    public AuditEventType? Action { get; set; }
    public string? Resource { get; set; }
    public List<PropertyChanged>? PropertyChanges { get; set; }
    public string? Notes { get; set; }
}

public class PropertyChanged
{
    public string PropertyName { get; set; } = string.Empty;
    public string InitialPropertyValue { get; set; } = string.Empty;
    public string NewPropertyValue { get; set; } = string.Empty;
}
