namespace LantanaGroup.Link.DemoApiGateway.Application.models.notification
{
    public class NotificationConfigurationModel
    {
        public Guid Id { get; set; } = Guid.Empty;
        public string FacilityId { get; set; } = string.Empty;
        public List<string>? EmailAddresses { get; set; }
        public List<EnabledNotification>? EnabledNotifications { get; set; }
        public List<FacilityChannel>? Channels { get; set; }
    }
}
