namespace LantanaGroup.Link.Shared.Application.Models.Kafka;
public class PropertyChangeModel
{
    public PropertyChangeModel() : this(string.Empty, string.Empty, string.Empty) { }

    public PropertyChangeModel(string name, string InitValue, string newValue)
    {
        PropertyName = name;
        InitialPropertyValue = InitValue;
        NewPropertyValue = newValue;
    }

    public string PropertyName { get; set; } = string.Empty;
    public string InitialPropertyValue { get; set; } = string.Empty;
    public string NewPropertyValue { get; set; } = string.Empty;
}

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
