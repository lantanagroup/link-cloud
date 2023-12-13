namespace LantanaGroup.Link.Normalization.Application.Models.Messages;

public class NotificationMessage
{
    public Key Key { get; set; }
    public Value Value { get; set; }
}

public class Key
{
    public string TenantId { get; set; }
    public string FacilityId { get; set; }
}
public class Value
{
    public string NotificationType { get; set; } = "NormalizationFailed";
    public string CorrelationId { get; set; }
    public string NotificationSubject { get; set; }
    public string NotificationBody { get; set; }
}
