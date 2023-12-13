
namespace LantanaGroup.Link.Notification.Domain.Entities
{
    /// <summary>
    /// This is for a future use case, not needed in current version of the notification service
    /// </summary>
    public class NotificationType : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public Guid? TemplateId { get; set; }
        public bool IsDeleted { get; set; }
    }
}
