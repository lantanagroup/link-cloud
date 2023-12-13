namespace LantanaGroup.Link.Notification.Domain.Entities
{
    [BsonCollection("notifications")]
    public class NotificationEntity : BaseEntity
    {
        public string NotificationType { get; set; } = string.Empty;
        public string? FacilityId { get; set; }
        public string? CorrelationId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public List<string> Recipients { get; set; } = new List<string>();
        public List<string>? Bcc { get; set; }
        public DateTime? SentOn { get; set; }

    }
}
