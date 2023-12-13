using LantanaGroup.Link.Notification.Domain.Entities;

namespace LantanaGroup.Link.Notification.Application.NotificationConfiguration.Commands
{
    public class CreateFacilityConfigurationModel
    {       
        public string FacilityId { get; set; } = string.Empty;
        public List<string>? EmailAddresses { get; set; }
        public List<EnabledNotification>? EnabledNotifications { get; set; }
        public List<FacilityChannel>? Channels { get; set; }
    }
}
