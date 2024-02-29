using System;

namespace LantanaGroup.Link.Notification.Domain.Entities
{
    public readonly record struct NotificationConfigId(Guid Value)
    {
        public static NotificationConfigId Empty => new(Guid.Empty);
        public static NotificationConfigId NewId() => new(Guid.NewGuid());
        public static NotificationConfigId FromString(string id) => new(new Guid(id));
    }

    //[BsonCollection("facilityConfigs")]
    public class NotificationConfig : BaseEntity
    {
        public NotificationConfigId Id { get; set; }
        public string FacilityId { get; set; } = string.Empty;
        public List<string>? EmailAddresses { get; set; }
        public List<EnabledNotification>? EnabledNotifications { get; set; }
        public List<FacilityChannel> Channels { get; set; } = new List<FacilityChannel>();

        public bool EmailAddressesEquals(List<string> originalEmailAddresses, List<string> currentEmailAddresses)
        {
            bool intersectsWith = originalEmailAddresses.Intersect(currentEmailAddresses, StringComparer.OrdinalIgnoreCase).Any();
            return intersectsWith;
        }        

    }

}
