using LantanaGroup.Link.Notification.Application.Models;

namespace LantanaGroup.Link.Notification.Application.Notification.Commands
{
    public class SendNotificationModel
    {
        public string Id { get; set; } = null!;
        public List<string> Recipients { get; set; } = new List<string>();
        public List<string>? Bcc { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationConfigurationModel? FacilityConfig { get; set; }
    }
}
