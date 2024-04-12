namespace LantanaGroup.Link.Notification.Domain.Entities
{
    public readonly record struct NotificationId(Guid Value)
    {
        public static NotificationId Empty => new(Guid.Empty);
        public static NotificationId NewId() => new(Guid.NewGuid());
        public static NotificationId FromString(string id) => new(new Guid(id));    
    }

    public class NotificationEntity : BaseEntity
    {
        public NotificationId Id { get; set; }
        public string NotificationType { get; set; } = null!;
        public string? FacilityId { get; set; }
        public string? CorrelationId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public List<string> Recipients { get; set; } = new List<string>();
        public List<string>? Bcc { get; set; }
        public List<DateTime> SentOn { get; set; } = new List<DateTime>();

    }
}
