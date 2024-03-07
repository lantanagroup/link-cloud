
namespace LantanaGroup.Link.Notification.Domain.Entities
{
    public class EnabledNotification
    {
        public string NotificationType { get; set; } = string.Empty;
        public List<string>? Recipients { get; set; }       
    }
}
