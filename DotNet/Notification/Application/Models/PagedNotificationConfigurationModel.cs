using LantanaGroup.Link.Notification.Application.Interfaces;

namespace LantanaGroup.Link.Notification.Application.Models
{
    public class PagedNotificationConfigurationModel : IPagedModel<NotificationConfigurationModel>
    {
        public List<NotificationConfigurationModel> Records { get; set; } = new List<NotificationConfigurationModel>();
        public PaginationMetadata Metadata { get; set; } = new PaginationMetadata();

        public PagedNotificationConfigurationModel() { }

        public PagedNotificationConfigurationModel(List<NotificationConfigurationModel> records, PaginationMetadata metadata)
        {
            Records = records;
            Metadata = metadata;
        }    
    }
}
