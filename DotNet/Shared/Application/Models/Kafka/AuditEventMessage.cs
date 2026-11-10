namespace LantanaGroup.Link.Shared.Application.Models.Kafka;
public class PropertyChangeModel
{
    public PropertyChangeModel() : this(string.Empty, string.Empty, string.Empty) { }

    public PropertyChangeModel(string name, string InitValue, string newValue, string? entity = null,
        string? rowKey = null)
    {
        PropertyName = name;
        InitialPropertyValue = InitValue;
        NewPropertyValue = newValue;
        Entity = entity;
        RowKey = rowKey;
    }

    public string PropertyName { get; set; } = string.Empty;
    public string InitialPropertyValue { get; set; } = string.Empty;
    public string NewPropertyValue { get; set; } = string.Empty;

    /// <summary>
    /// The entity type whose row this change belongs to, so one event can describe an update that
    /// touched several rows.
    /// </summary>
    /// <remarks>
    /// This is the schema's name for the table, not the audit resource vocabulary that the event's
    /// <see cref="AuditEventMessage.Resource"/> uses. Null on a change a handler added itself, and
    /// on every event produced before this field existed.
    /// </remarks>
    public string? Entity { get; set; }

    /// <summary>
    /// The primary key of the row this change belongs to, so the changes of a bulk update can be
    /// told apart. Null where the producer did not know it.
    /// </summary>
    /// <remarks>
    /// Composite keys are joined with '|' in the order the model declares them, so a natural-key
    /// part containing '|' makes that join ambiguous. A key one of the masking rules catches reads
    /// <c>[redacted]</c>, which leaves the rows of such an entity indistinguishable rather than
    /// publishing a key that identifies a person.
    /// </remarks>
    public string? RowKey { get; set; }
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
    public string? ResourceId { get; set; }
    public string? PatientId { get; set; }
    public List<PropertyChangeModel>? PropertyChanges { get; set; }
    public string? Notes { get; set; }
}
