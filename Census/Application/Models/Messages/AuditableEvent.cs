using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Census.Application.Models.Messages;

//X-Correlation-Id should be kafka header when producing event
public class AuditableEvent
{
    public string Key { get; set; }
    public string CorrelationId { get; set; }
    public AuditableEventValue Value { get; set; }
}

public class AuditableEventValue
{
    public string ServiceName { get; set; }
    public DateTime EventDate { get; set; } = DateTime.UtcNow;
    public string UserId { get; set; }
    public string User { get; set; } = string.Empty;
    public AuditEventType Action { get; set; } = AuditEventType.Query; //allowed values: Create, Update, Delete, Query, Submit
    public string Resource { get; set; } = string.Empty;
    public string PropertyChanges { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
