namespace LantanaGroup.Link.DemoApiGateway.Application.models.notification
{
    public class EnabledNotification
    {
        public string NotificationType { get; set; } = string.Empty;
        public List<string>? Recipients { get; set; }
    }
}
