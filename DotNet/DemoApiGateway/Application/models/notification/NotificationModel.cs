namespace LantanaGroup.Link.DemoApiGateway.Application.models.notification
{
    public class NotificationModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string NotificationType { get; set; } = string.Empty;
        public string? FacilityId { get; set; }
        public string? CorrelationId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public List<string> Recipients { get; set; } = new List<string>();
        public List<string>? Bcc { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? SentOn { get; set; }
    }
}
