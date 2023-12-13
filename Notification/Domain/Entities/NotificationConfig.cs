
namespace LantanaGroup.Link.Notification.Domain.Entities
{
    [BsonCollection("facilityConfigs")]
    public class NotificationConfig : BaseEntity
    {
        public string FacilityId { get; set; } = string.Empty;
        public List<string>? EmailAddresses { get; set; }
        public List<EnabledNotification>? EnabledNotifications { get; set; }
        public List<FacilityChannel> Channels { get; set; } = new List<FacilityChannel>();

    }
}
