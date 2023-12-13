using LantanaGroup.Link.Notification.Domain.Entities;

namespace LantanaGroup.Link.Notification.Application.Models
{
    public class NotificationConfigurationModel
    {
        public string Id { get; set; } = Guid.Empty.ToString();
        public string FacilityId { get; set; } = string.Empty;
        public List<string>? EmailAddresses { get; set; }
        public List<EnabledNotification>? EnabledNotifications { get; set; }
        public List<FacilityChannel>? Channels { get; set; }
    }
}
